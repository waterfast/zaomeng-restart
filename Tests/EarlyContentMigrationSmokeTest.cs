using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Combat.Effects;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Skills;
using Zaomeng.UI;
using Zaomeng.UI.MainMenu;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

/// <summary>使用内存会话验证迁入内容、真实装备请求、命中窗口与地图切换，不写玩家存档。</summary>
public partial class EarlyContentMigrationSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			TranslationServer.SetLocale("zh_CN");
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			catalog.Validate();
			string[] ids = "ptxzg yxxzg whg jcbj qld ryjgb ptxzf dsyj xwj ptsmz dsqz bhz jhcz ptjs bsp jcjs qlp ptdp wtp jcjp zhp jcdp ptcs dszy pxk ptyyc jmc jcmc hdc jlxmc ptcp dshp bxp pttq dslq twq qthbq ptcf zhj jclj ttj dslj jcsz zqj qxsh".Split(' ');
			foreach (string id in ids)
				Check(catalog.TryGetDefinition(id, out var item) && item is EquipmentDefinition && item.Icon is not null && item.Description != item.DescriptionKey,
					$"registered early equipment with icon and lore: {id}");
			catalog.TryGetDefinition("dsqz", out var staff);
			Check(!((EquipmentDefinition)staff!).CanEquip(new SaveCharacter { Id = "role_1" }) && ((EquipmentDefinition)staff).CanEquip(new SaveCharacter { Id = "role_2" }), "migrated equipment respects original owner");
			var character = new SaveCharacter { Id = "role_1", BaseStats = new() { MaxHealth = 1000, MaxMana = 1000, Attack = 100 } };
			character.LearnSkill("lyfb");
			var bag = new InventoryService(10, catalog);
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.BindCharacter(character, catalog); player.InputEnabled = false; AddChild(player);
			player.SetPhysicsProcess(false); player.AttackBox.SetPhysicsProcess(false);
			var target = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			target.MaxHealth = 100000; target.AiEnabled = false; AddChild(target); target.SetPhysicsProcess(false);
			target.DodgeRating = 0; target.MagicDefense = 0; target.Level = player.Level;
			var storm = GD.Load<SkillDefinition>("res://Content/Skills/Wukong/lyfb.tres");
			float before = target.Health;
			CombatResolver.Resolve(player, target, storm.Hits[0].Hit!, 1, storm);
			float baseDamage = before - target.Health;
			var events = new GameplayEvents { EquipmentRequested = new(), EquipmentChanged = new(), InventoryChanged = new(), SaveFailed = new(), GemRequested = new(), EquipmentModified = new(), ItemActionRequested = new(), ItemActionCompleted = new() };
			using var binding = new GameplayEquipmentBinding(character, catalog, bag, events, player.RefreshCharacterStats, () => {}, new Wallet());
			bag.AddItem("dsyj", 1);
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, bag.Slots[0]!.Equipment!.InstanceId)).Success, "real request equips earth armor");
			Check(player.GrantedEquipmentSkills.Single().DisplayName == "地煞怒火" && player.GrantedEquipmentSkills[0].Description.Contains("50%"), "earth fury name and parameter come from skill definition");
			for (int i = 0; i < 3; i++) player.RefreshCharacterStats();
			Check(player.TryUseSkill(storm), "real flame storm cast starts");
			var hurtbox = target.GetNode<HurtBox>("HurtBox");
			foreach (HitEvent wave in storm.Hits)
			{
				player.Animator.Seek((wave.StartFrame + 0.1) / storm.FramesPerSecond, true);
				player._PhysicsProcess(0);
				before = target.Health;
				player.AttackBox.EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
				Check(Near(before - target.Health, baseDamage * 1.5f), "each real hit window applies 50 percent once");
				player.AttackBox.EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
				Check(Near(before - target.Health, baseDamage * 1.5f), "same target cannot repeat within one window");
			}
			before = target.Health;
			CombatResolver.Resolve(player, target, storm.Hits[0].Hit!, 1);
			Check(Near(before - target.Health, baseDamage), "normal attack source receives no skill bonus");
			var other = GD.Load<SkillDefinition>("res://Content/Skills/Wukong/slz.tres");
			before = target.Health;
			CombatResolver.Resolve(player, target, storm.Hits[0].Hit!, 1, other);
			Check(Near(before - target.Health, baseDamage), "other skill receives no storm bonus");
			var delayed = GD.Load<PackedScene>("res://Content/Skills/Behaviors/FireEyesImpact.tscn").Instantiate<FireEyesEffect>();
			delayed.Configure(player, target, 1, GD.Load<SkillDefinition>("res://Content/Skills/Wukong/hyjj.tres"));
			AddChild(delayed); delayed.SetPhysicsProcess(false);
			int expected = LegacyDamageCalculator.Calculate(player, target, player.Attack * delayed.Hit.MinimumMultiplier(1) + delayed.Hit.FlatDamage, delayed.Hit.DamageType, false, 1, 1).Damage;
			before = target.Health; delayed._PhysicsProcess(0);
			Check(Near(before - target.Health, expected), "delayed hit keeps fire-eyes source while actor is casting storm");
			delayed.QueueFree();
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip, Slot: EquipmentSlot.Armor)).Success, "real request removes earth armor");
			before = target.Health; CombatResolver.Resolve(player, target, storm.Hits[0].Hit!, 1, storm);
			Check(Near(before - target.Health, baseDamage) && player.GrantedEquipmentSkills.Count == 0, "unequip removes storm damage bonus immediately");
			new SkillParameterEffect { TargetSkills = [storm], AffectsNormalAttacks = false, Modifiers = new() { DamageMultiplier = 1.5f } }.Validate();
			player.QueueFree(); target.QueueFree();
			await VerifyMaps(catalog);
			GD.Print("EARLY CONTENT MIGRATION SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private async Task VerifyMaps(ItemCatalog catalog)
	{
		await Frame();
		var data = new GameSaveData();
		var underworld = GD.Load<WorldMapDefinition>("res://Content/Maps/Underworld.tres");
		var heaven = GD.Load<WorldMapDefinition>("res://Content/Maps/Heaven.tres");
		foreach (WorldMapDefinition destination in new[] { underworld, heaven })
		{
			bool development = destination.AllowDevelopmentAccess;
			destination.AllowDevelopmentAccess = false;
			Check(!WorldMapAccess.CanEnter(destination, data), "unbeaten boss blocks destination");
			data.DefeatedBossIds.Add(destination.RequiredBossId);
			Check(WorldMapAccess.CanEnter(destination, data), "required boss defeat unlocks destination");
			destination.AllowDevelopmentAccess = development;
		}
		var mapBag = new InventoryService(10, catalog);
		data = InventorySaveMapper.Capture(mapBag);
		data.DefeatedBossIds.Add(underworld.RequiredBossId);
		var serializer = new Zaomeng.Save.Serialization.JsonSaveSerializer();
		Check(serializer.Deserialize(serializer.Serialize(data)).DefeatedBossIds.Contains(underworld.RequiredBossId), "boss defeat state survives save serialization");
		data.DefeatedBossIds.Clear();
		var hero = new SaveCharacter { Id = "role_1", Name = "悟空" };
		Zaomeng.Character.CharacterProgression.SyncBaseStats(hero); data.Characters.Add(hero);
		typeof(GameSession).GetProperty("Slot")!.SetValue(null, 0);
		typeof(GameSession).GetProperty("Data")!.SetValue(null, data);
		typeof(GameSession).GetProperty("Inventory")!.SetValue(null, mapBag);
		var map = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/Map1.tscn").Instantiate<WorldMap>();
		GetTree().Root.AddChild(map); GetTree().CurrentScene = map;
		await Switch("next", "Map_2");
		Check(GetTree().CurrentScene.GetNode<BaseButton>("Level_11").Disabled, "underworld level remains disabled");
		GetTree().CurrentScene.GetNode<BaseButton>("Learn_skill").EmitSignal(BaseButton.SignalName.Pressed);
		Check(GetTree().CurrentScene.GetChildren().OfType<MenuManager>().Single().IsMenuOpen, "underworld opens shared learning menu");
		GetTree().CurrentScene.GetChildren().OfType<MenuManager>().Single().CloseMenu();
		await Capture("underworld-map");
		await Switch("Tianting/Changetott", "Map_3");
		Check(GetTree().CurrentScene.GetNode<BaseButton>("Level_21").Disabled, "heaven level remains disabled");
		await Capture("heaven-map");
		await Switch("Button", "Map_2");
		await Switch("next", "Map_1");
		await Switch("next", "Map_2");
		await Switch("Tianting/Changetott", "Map_3");
		await Switch("next", "Map_1");
		underworld.AllowDevelopmentAccess = false;
		GetTree().CurrentScene.GetNode<BaseButton>("next").EmitSignal(BaseButton.SignalName.Pressed);
		await Frame();
		Check(GetTree().CurrentScene.Name == "Map_1", "portal rechecks unlock and refuses scene transition");
		underworld.AllowDevelopmentAccess = true;
	}
	private async Task Switch(string button, string expected)
	{
		GetTree().CurrentScene.GetNode<BaseButton>(button).EmitSignal(BaseButton.SignalName.Pressed);
		await Frame(); await Frame();
		Check(GetTree().CurrentScene.Name == expected, $"portal switches to {expected}");
	}
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	private async Task Capture(string name)
	{
		if (!OS.GetCmdlineUserArgs().Contains("--capture-migration")) return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string directory = ProjectSettings.GlobalizePath("res://.godot/migration-checks");
		System.IO.Directory.CreateDirectory(directory);
		GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
	}
	private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.01f;
	private static void Check(bool condition, string message)
	{ if (!condition) throw new InvalidOperationException(message); GD.Print("PASS: " + message); }
}
