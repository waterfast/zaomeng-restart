using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.Skills;
using Zaomeng.UI;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

public partial class SkillCatalogSmokeTest : Node
{
	public override void _Ready() => GetTree().Root.CallDeferred(Node.MethodName.AddChild, new SkillCatalogTestRunner());
}

/// <summary>跨场景注册与技能行为测试；槽位0隔离玩家存档。</summary>
public partial class SkillCatalogTestRunner : Node
{
	private ItemCatalog _items = null!;
	private SaveCharacter _character = null!;
	private SkillLearningService _learning = null!;
	private readonly bool _capture = OS.GetCmdlineUserArgs().Contains("--capture-skills");
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			_items = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			_items.Validate();
			CheckInvalidRegistrations();
			Prepare("role_1");
			GameplayLevel level = await EnterForest();
			Player player = level.GetNode<Player>("Player");
			CheckPassiveLearning(player);
			float health = player.Health, mana = player.Mana;
			player.ReceiveHit(new HitResult(30, Vector2.Zero, 0, DamageType.True));
			player.TrySpendMana(30);
			await Delay(1.15);
			Check(player.Health >= health - 23 && player.Mana >= mana - 23, "passives actually restore health and mana each second");
			await CapturePanel(level, true, "wukong-passives");
			Check(_learning.TryLearn("jdy", out _) && _learning.TryLearn("hmz", out _), "special skills learn from catalog");
			_learning.Apply(player);
			foreach (string id in new[] { "jdy", "hmz" })
			{
				player.ResetForSpawn(new(550, 502));
				player.RestoreMana(1000);
				Check(player.TryUseSkill(_learning.Catalog.Find(id)!.Action), $"start interrupt test {id}");
				await Delay(0.05);
				player.ReceiveHit(new HitResult(1, Vector2.Zero, 0.1f, DamageType.True));
				// 火魔斩原版自带无敌，验证强制重置；筋斗云验证实际受击中断。
				if (id == "hmz")
				{
					Check(player.State == ActorState.Attacking, "fire slash preserves configured invulnerability");
					player.ResetForSpawn(player.Position);
				}
				await Delay(0.05);
				Check(player.Gravity == 600 && !player.GetChildren().OfType<SkillBehavior>().Any(), $"interrupted {id} restores gravity and frees behavior");
			}
			await Delay(0.85);
			player.ResetForSpawn(new(550, 502)); player.RestoreMana(1000);
			var fireSlash = _learning.Catalog.Find("hmz")!.Action!;
			Check(player.TryUseSkill(fireSlash) && player.GetSkillCooldown(fireSlash) == 0, "fire slash starts with pending cooldown in actual level");
			bool landedCooldown = false;
			for (int tick = 0; tick < 40 && !landedCooldown; tick++)
			{
				await Delay(0.05);
				landedCooldown = player.GetSkillCooldown(fireSlash) > 0;
			}
			Check(landedCooldown && player.IsOnFloor() && player.GetSkillCooldown(fireSlash) <= 0.8f,
				"landing starts real fire slash countdown");
			GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/ChoosePlayer.tscn");
			await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
			Node selection = GetTree().CurrentScene;
			selection.GetNode<Button>("Bg/ScrollContainer/PlayerList/role_2").EmitSignal(BaseButton.SignalName.Pressed);
			Check(selection.GetNode<Label>("Bg/Bg/RoleName").Text == "唐僧" &&
				selection.GetNode<Label>("Bg/ScrollContainer/PlayerList/role_2/RoleName").Text == "唐僧" &&
				selection.GetNode<AnimationPlayer>("Bg/SpecialEffectPlayer").CurrentAnimation == "Role_2", "second character selects its own name, portrait and animation");
			await Capture("tangseng-selection");
			Prepare("role_2");
			Check(!_learning.TryLearn("hytj", out _) && !_learning.TryEquip(0, "hytj", out _), "foreign character skills rejected");
			Check(_learning.TryLearn("blb", out _) && _learning.TryLearn("tjgl", out _), "Tangseng active skills learn generically");
			Check(_learning.TryEquip(0, "blb", out _) && _learning.TryEquip(1, "tjgl", out _) && _learning.TrySetKey(0, Key.W, out _), "Tangseng can equip skills and use W without cloud-flight conflict");
			var serializer = new JsonSaveSerializer();
			GameSaveData restored = serializer.Deserialize(serializer.Serialize(GameSession.Data!));
			SetSessionProperty("Data", restored);
			_character = restored.Characters.Single();
			_learning = new SkillLearningService(_character, restored.Wallet);
			Check(restored.CurrentCharacterId == "role_2" && _character.SkillKeyCodes[0] == (long)Key.W, "selected character, learning and keys survive serialization");
			level = await EnterForest();
			player = level.GetNode<Player>("Player");
			Check(player.SceneFilePath.EndsWith("Tangseng.tscn") && player.CharacterId == "role_2" && player.MaxHealth == 50 && player.MaxMana == 100, "level spawns registered Tangseng actor with its base stats");
			_learning.Apply(player);
			await CapturePanel(level, false, "tangseng-skills");
			Check(level.GetNode<TextureRect>("OldHud/roleLayer/role_menu/SkillBox/Y/Icon").Texture == _learning.Catalog.Find("blb")!.Icon &&
				level.GetNode<Label>("OldHud/roleLayer/role_menu/SkillBox/Y/Y").Text == "W", "HUD shows registered skill icon and custom binding");
			player.ResetForSpawn(new(500, 502));
			player.Face(1);
			var target = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			target.AiEnabled = false;
			target.MaxHealth = 1000;
			level.AddChild(target);
			target.ResetForSpawn(new(760, 502));
			target.SetPhysicsProcess(false);
			Check(player.TryUseSkill(_learning.Catalog.Find("blb")!.Action) && player.Mana == 85, "ice dragon starts and charges registered mana");
			await Delay(0.6);
			Check(level.GetChildren().OfType<SkillProjectile>().Any(), "ice dragon creates independent projectile");
			await Capture("tangseng-ice-dragon");
			await Delay(0.65);
			Check(target.Health < 1000 && player.State == ActorState.Free, "ice dragon damages target and finishes cast");
			Check(player.TryAttack(), "Tangseng normal attack starts");
			await Delay(0.35);
			await Capture("tangseng-normal");
			await Delay(0.6);
			Check(target.Health < 1000 - 8, "Tangseng normal attack projectile also damages target");
			target.QueueFree();
			player.ReceiveHit(new HitResult(35, Vector2.Zero, 0, DamageType.True));
			await Delay(0.25);
			Check(player.TryUseSkill(_learning.Catalog.Find("tjgl")!.Action) && player.Mana == 50, "healing skill charges mana");
			await Delay(0.8);
			Check(player.Health == 29, "healing uses maximum health and missing-health bonus");
			await Capture("tangseng-healing");
			await Delay(0.9);
			Check(player.State == ActorState.Free, "healing releases attack state");
			Check(_learning.TryLearn("yh", out _), "common passive available to second character");
			_learning.Apply(player);
			Check(player.CalculatedStats!.HealthRegeneration == 2 && _learning.Level("yh") == 1, "shared resource keeps per-character independent levels");
			player.ReceiveHit(new HitResult(10000, Vector2.Zero, 0, DamageType.True));
			await Delay(0.05);
			Check(player.GetNode<AnimatedSprite2D>("Facing/Visual/Death") is { Visible: true } death && death.IsPlaying(), "Tangseng plays migrated death effect");
			await Capture("tangseng-death");
			GD.Print("SKILL CATALOG SMOKE TEST PASSED");
			GetTree().CurrentScene.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GetTree().Quit();
		}
		catch (Exception error) { GetTree().Paused = false; GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void CheckInvalidRegistrations()
	{
		SkillCatalogRegistry registry = SkillCatalogRegistry.Default;
		Check(registry.Catalogs.Count == 2 && registry.Get("role_1").Skills.Count == 13 && registry.Get("role_2").Skills.Count == 5, "explicit registry contains both character catalogs");
		var duplicate = new SkillCatalogRegistry { Catalogs = new() { registry.Get("role_1"), registry.Get("role_1") } };
		Reject(duplicate.Validate, "duplicate character rejected");
		SkillCatalog copied = (SkillCatalog)registry.Get("role_2").Duplicate();
		copied.Skills = new(copied.Skills);
		copied.Skills.Add(copied.Skills[0]);
		Reject(copied.Validate, "duplicate skill rejected");
		SkillEntry bad = (SkillEntry)registry.Get("role_2").Skills[0].Duplicate();
		bad.Action = (SkillDefinition)bad.Action!.Duplicate();
		bad.Action.Growth = (SkillGrowth)bad.Action.Growth.Duplicate();
		bad.Action.Growth.InitialLearningCost = 0;
		Reject(bad.Validate, "invalid learning table rejected");
		bad.Action.Growth.InitialLearningCost = 100;
		bad.Icon = null!;
		bad.Validate();
		Check(bad.DisplayIcon is not null, "missing icon uses black default");
		var missingProjectile = new ProjectileSkillBehavior();
		Reject(missingProjectile.ValidateConfiguration, "missing behavior dependency rejected");
		missingProjectile.Free();
		SkillCatalog missingAnimation = (SkillCatalog)registry.Get("role_2").Duplicate();
		missingAnimation.Skills = new(missingAnimation.Skills);
		SkillEntry copiedEntry = (SkillEntry)missingAnimation.Skills[0].Duplicate();
		copiedEntry.Action = (SkillDefinition)copiedEntry.Action!.Duplicate();
		copiedEntry.Action.Animation = "not_registered";
		missingAnimation.Skills[0] = copiedEntry;
		Reject(missingAnimation.Validate, "missing actor animation rejected");
		SaveCharacter invalid = new() { Id = "role_2" };
		invalid.LearnSkill("hytj");
		Reject(() => registry.Get("role_2").ValidateCharacter(invalid), "foreign saved skill rejected");
	}
	private void CheckPassiveLearning(Player player)
	{
		foreach (string id in new[] { "kb", "yh", "hh" })
		{
			long start = _learning.Souls;
			for (int i = 0; i < 6; i++) Check(_learning.TryLearn(id, out _), $"learn {id} level {i + 1}");
			Check(start - _learning.Souls == 105000 && !_learning.TryLearn(id, out _) && !_learning.TryEquip(0, id, out _), $"{id} preserves old costs, max level and passive-only rule");
		}
		_learning.Apply(player);
		float critical = player.CriticalRating;
		_learning.Apply(player);
		Check(critical == 7 && player.CriticalRating == 7 && player.CalculatedStats!.HealthRegeneration == 7 && player.CalculatedStats.ManaRegeneration == 7, "passive recalculation does not stack bonuses");
		Check(!_learning.TrySetKey(0, Key.W, out _), "Wukong reserves W for cloud flight");
	}
	private void Prepare(string characterId)
	{
		_character = new() { Id = characterId, Name = SkillCatalogRegistry.Default.Get(characterId).DisplayName };
		CharacterProgression.SyncBaseStats(_character);
		var inventory = new InventoryService(70, _items);
		GameSaveData data = InventorySaveMapper.Capture(inventory);
		data.CurrentCharacterId = characterId;
		data.Characters.Add(_character);
		data.Wallet.Souls = 500000;
		SetSessionProperty("Slot", 0);
		SetSessionProperty("Inventory", inventory);
		SetSessionProperty("Data", data);
		_learning = new SkillLearningService(_character, data.Wallet);
	}
	private static void SetSessionProperty(string property, object value) => typeof(GameSession).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
	private async Task<GameplayLevel> EnterForest()
	{
		GetTree().ChangeSceneToFile(GameSession.FirstLevel);
		await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
		var level = (GameplayLevel)GetTree().CurrentScene;
		level.GetChildren().OfType<ForestEncounter>().Single().SetPhysicsProcess(false);
		level.GetNode<Player>("Player").InputEnabled = false;
		await Delay(0.1);
		return level;
	}
	private async Task CapturePanel(GameplayLevel level, bool passive, string name)
	{
		var menu = level.GetNode<MenuManager>("MenuManager");
		menu.ToggleMenu("skills_menu");
		Node2D panel = level.GetNode("HUD").GetChildren().OfType<Node2D>().Single(node => node.SceneFilePath.EndsWith("Learn_skill.tscn"));
		if (passive) panel.GetNode<Button>("bg/bd_skill").EmitSignal(BaseButton.SignalName.Pressed);
		await Capture(name);
		menu.CloseMenu();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private async Task Capture(string name)
	{
		if (!_capture) return;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string directory = ProjectSettings.GlobalizePath("res://.godot/skill-checks");
		System.IO.Directory.CreateDirectory(directory);
		GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
	}
	private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, processInPhysics: true), SceneTreeTimer.SignalName.Timeout);
	private static void Reject(Action action, string message)
	{
		bool rejected = false;
		try { action(); } catch (InvalidOperationException) { rejected = true; }
		Check(rejected, message);
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
