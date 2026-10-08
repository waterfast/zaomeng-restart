using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Character;
using Zaomeng.Equipment;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Quests;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.Skills;
using Zaomeng.UI;
using Zaomeng.UI.Inventory;
using Zaomeng.UI.Quests;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

/// <summary>内存数据验证实际界面按钮、资源注册和提交边界，不读写玩家存档。</summary>
public partial class ContentSystemsSmokeTest : Node2D
{
	private readonly bool _capture = OS.GetCmdlineUserArgs().Contains("--capture-content");
	public override async void _Ready()
	{
		try
		{
			TranslationServer.SetLocale("zh_CN");
			var items = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres"); items.Validate();
			var stats = GD.Load<CharacterStatCatalog>("res://Content/GameData/Stats/Registry.tres");
			Check(stats.CreateRegistry().Entries.Count == 18 && stats.Definitions.All(def => TranslationServer.Translate(def.DescriptionKey) != def.DescriptionKey), "all registered attributes have translated descriptions");
			Check(Near(SkillCooldownCalculator.Calculate(6, 100), 3) && Near(SkillCooldownCalculator.Calculate(6, -10), 6), "haste formula and negative clamp");
			var character = new SaveCharacter { Id = "role_1", Level = 10, BaseStats = new() { MaxHealth = 1000, MaxMana = 1000, Attack = 30, HasteRating = 100, SkillLevelBonus = 2 } };
			var wallet = new Wallet { Souls = 50000 };
			var learning = new SkillLearningService(character, wallet);
			Check(learning.TryLearn("slz", out _) && learning.TryLearn("slz", out _) && learning.Level("slz") == 2 && wallet.Souls == 49300, "active learning advances and uses independent growth costs");
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.InputEnabled = false; player.BindCharacter(character, items); AddChild(player); player.SetPhysicsProcess(false);
			var action = learning.Catalog.Find("slz")!.Action!;
			Check(action.GetManaCost(4) == 37 && Near(action.CooldownSeconds, 2.4f), "growth resource supplies old mana formula and new base cooldown in seconds");
			Check(player.EffectiveSkillLevel("slz") == 4 && player.EffectiveSkillLevel("hytj") == 0, "level bonus only applies to learned skills");
			float mana = player.Mana;
			Check(player.TryUseSkill(action) && Near(player.Mana, mana - action.GetManaCost(4)), "cast charges effective-level mana");
			float cd = player.GetSkillCooldown(action);
			Check(Near(cd, action.CooldownSeconds / 2) && !player.TryUseSkill(action), "successful cast starts haste cooldown and repeated input is rejected");
			var updateResources = typeof(Player).GetMethod("UpdateResources", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
			for (int tick = 0; tick < 10; tick++) updateResources.Invoke(player, new object[] { 0.01f });
			Check(Near(player.GetSkillCooldown(action), cd - 0.1f), "cooldown decreases by elapsed seconds without a second legacy conversion");
			character.PermanentBonuses.HasteRating = 100; player.RefreshCharacterStats();
			Check(Near(player.GetSkillCooldownDuration(action), cd), "stat refresh preserves running cooldown duration");
			player.ResetForSpawn(Vector2.Zero);
			Check(action.Hits[0].Hit!.MinimumMultiplier(2) > action.Hits[0].Hit!.MinimumMultiplier(1), "hit growth uses level");
			Check(SkillLevelResolver.Effective(9, 100, 10) == 10 && SkillLevelResolver.Effective(2, -10, 10) == 2, "effective level caps at skill maximum and ignores negative bonuses");
			Check(learning.TryLearn("lys", out _) && learning.TryLearn("hmz", out _), "learn cooldown timing fixtures");
			var cooldownPlayer = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			cooldownPlayer.InputEnabled = false; cooldownPlayer.BindCharacter(character, items); AddChild(cooldownPlayer); cooldownPlayer.SetPhysicsProcess(false);
			var dash = learning.Catalog.Find("lys")!.Action!;
			var slash = learning.Catalog.Find("hmz")!.Action!;
			Check(Near(dash.CooldownSeconds, 2.8f) && Near(slash.CooldownSeconds, 4.8f), "dash and slash register independent base cooldowns in seconds");
			Check(cooldownPlayer.TryUseSkill(dash) && Near(cooldownPlayer.GetSkillCooldown(slash), 0), "dash does not start fire slash cooldown");
			cooldownPlayer.ResetForSpawn(Vector2.Zero);
			updateResources.Invoke(cooldownPlayer, new object[] { 1f });
			Check(cooldownPlayer.TryUseSkill(slash) && Near(cooldownPlayer.GetSkillCooldown(slash), 0), "fire slash defers its countdown");
			float pendingDuration = cooldownPlayer.GetSkillCooldownDuration(slash);
			updateResources.Invoke(cooldownPlayer, new object[] { 1f });
			Check(!cooldownPlayer.TryUseSkill(slash) && Near(cooldownPlayer.GetSkillCooldownDuration(slash), pendingDuration), "pending cooldown is locked and does not count down before impact");
			cooldownPlayer.GetChildren().OfType<SkillBehavior>().Single().Stop();
			Check(Near(cooldownPlayer.GetSkillCooldown(slash), pendingDuration), "interruption starts snapshotted cooldown instead of bypassing it");
			cooldownPlayer.QueueFree();
			var target = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			target.AiEnabled = false; target.MaxHealth = 1000; AddChild(target); target.SetPhysicsProcess(false);
			var hit = (HitDefinition)action.Hits[0].Hit!.Duplicate(); hit.CanCrit = false;
			CombatResolver.Resolve(player, target, hit, 1); float firstDamage = 1000 - target.Health;
			target.ResetForSpawn(Vector2.Zero);
			CombatResolver.Resolve(player, target, hit, 2);
			Check(1000 - target.Health > firstDamage, "actual combat damage increases with registered skill growth");
			target.QueueFree();
			var events = new GameplayEvents { EquipmentRequested = new(), EquipmentChanged = new(), InventoryChanged = new(), SaveFailed = new(), GemRequested = new(), EquipmentModified = new(), ItemActionRequested = new(), ItemActionCompleted = new() };
			var inventory = new InventoryService(70, items);
			inventory.AddItem("ptxzg", 1); inventory.AddItem("ryjgb", 1); inventory.AddItem("dshl", 1);
			int saves = 0;
			using var binding = new GameplayEquipmentBinding(character, items, inventory, events, player.RefreshCharacterStats, () => saves++, wallet);
			using var adapter = new InventoryViewAdapter(inventory, items);
			var backpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackPack.tscn").Instantiate<Node2D>(); AddChild(backpack);
			using var magicView = new Zaomeng.UI.Inventory.MagicWeaponView(this, items, character, events);
			magicView.CloseRequested += magicView.Hide;
			var view = new LegacyBackpackView(backpack, adapter, items, character, wallet, player, () => saves++, events, magicView.Show);
			backpack.GetNode<BaseButton>("background/infomation/second").EmitSignal(BaseButton.SignalName.Pressed);
			Check(backpack.GetNode<Label>("background/infomation/crit/Crit_tt").Text == "极速" && backpack.GetNode<Label>("background/infomation/crit").TooltipText.Contains("冷却"), "second stat page shows haste and hover explanation");
			await Capture("attributes");
			var grid = backpack.GetNode<GridContainer>("Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box");
			Check(grid.Columns == 5 && grid.GetChildCount() == 25, "backpack shows five rows and five columns per page");
			var initialSlots = inventory.Slots;
			Check(inventory.AddItem("ptxzg", 100) && inventory.Capacity > 70, "bag grows beyond seventy equipment slots");
			var nextPage = backpack.GetNode<BaseButton>("Main_Backpack/ChangePage/NextPage");
			for (int page = 0; page < 4; page++) nextPage.EmitSignal(BaseButton.SignalName.Pressed);
			Check(backpack.GetNode<Label>("Main_Backpack/ChangePage/CurrentPageText").Text == "5/5"
				&& nextPage.Disabled && !grid.GetChild<Button>(0).Disabled, "grown inventory displays the final page through existing page controls");
			inventory.RestoreSlots(initialSlots);
			grid.GetChildren().OfType<Button>().ElementAt(1).EmitSignal(Control.SignalName.MouseEntered);
			await Capture("equipment-skill");
			grid.GetChildren().OfType<Button>().ElementAt(1).EmitSignal(Control.SignalName.MouseExited);
			backpack.GetNode<BaseButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/pl_sell").EmitSignal(BaseButton.SignalName.Pressed);
			await Capture("bulk-sale");
			long before = wallet.Souls;
			backpack.GetNode<BaseButton>("BulkSale/MarginContainer/VBoxContainer/qd").EmitSignal(BaseButton.SignalName.Pressed);
			Check(!inventory.Slots.Any(stack => stack?.ItemId == "ptxzg") && inventory.Slots.Any(stack => stack?.ItemId == "ryjgb") && wallet.Souls > before && saves == 1, "actual bulk button sells white gear, preserves higher quality, saves once");
			backpack.GetNode<AcceptDialog>("EquipmentFeedback").Hide();
			inventory.AddItem("ptxzg", 1); wallet.Souls = long.MaxValue;
			var overflow = events.ItemActionRequested.Send(new("sell_white_equipment", new(-1, "")));
			Check(!overflow.Success && inventory.Slots.Any(stack => stack?.ItemId == "ptxzg"), "bulk overflow leaves gear intact"); wallet.Souls = before;
			var protectedGear = new EquipmentDefinition { Id = "test_socket_white", GemSocketCount = 1, SellPrice = 10 };
			items.Definitions.Add(protectedGear);
			items.Definitions.Add(new GemDefinition { Id = "fixture_gem", Category = ItemCategory.Material });
			var protectedBag = new InventoryService(1, items);
			var instance = EquipmentInstance.Create(protectedGear.Id, 1).WithGem(0, "fixture_gem");
			protectedBag.RestoreSlots(new[] { new ItemStack(protectedGear.Id, 1, instance) });
			Check(!new BulkEquipmentSale(protectedBag, items, wallet).Execute().Success && protectedBag.Slots[0]!.Equipment == instance,
				"socketed white equipment is preserved by bulk sale");
			backpack.GetNode<BaseButton>("background/infomation/equ_/VBoxContainer/fb").EmitSignal(BaseButton.SignalName.Pressed);
			await Capture("magic-weapons");
			var magic = magicView.Root;
			Check(magic.SceneFilePath.EndsWith("Magic_weapon_infor.tscn") && magic.GetNode<Label>("BG/Name_").Text == "未装备法宝", "magic slot opens original detail panel with empty state");
			int gourdSlot = Enumerable.Range(0, inventory.Capacity).First(index => inventory.Slots[index]?.ItemId == "dshl");
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, gourdSlot, inventory.Slots[gourdSlot]!.Equipment!.InstanceId), this).Success, "gourd equips through existing backpack request");
			Check(magic.GetNode<Label>("BG/Name_").Text == "地煞葫芦" && magic.GetNode<Label>("BG/MagicWeaponSkillTitle").Text == "烈焰火柱", "magic detail refreshes original title and skill description");
			Check(magic.GetNode<Sprite2D>("BG/Icon_").Texture.ResourcePath.EndsWith("MagicWeapon/Skill_Icon/dshl.png") && magic.GetNode<AnimationPlayer>("BG/IconPlayer").CurrentAnimation == "dshl", "magic detail uses original skill icon and display animation");
			Check(magic.GetNode<BaseButton>("BG/bg_2/up_level").Disabled && magic.GetNode<BaseButton>("BG/szfb").Disabled && magic.GetNode<Label>("BG/bg_2/m_czl").Text == "—", "unimplemented magic operations disabled without fake growth values");
			await Capture("magic-detail");
			magic.GetNode<BaseButton>("BG/tips").EmitSignal(BaseButton.SignalName.Pressed);
			Check(GetNode<Node2D>("MagicWeaponHelp").Visible, "original magic help opens");
			GetNode<BaseButton>("MagicWeaponHelp/BG/Close").EmitSignal(BaseButton.SignalName.Pressed);
			magic.Hide(); backpack.Hide();
			var skillRoot = GD.Load<PackedScene>("res://Scenes/UI/Skill/Learn_skill.tscn").Instantiate<Node2D>(); AddChild(skillRoot);
			var skills = new LegacySkillPanel(); skills.Bind(skillRoot, learning, () => {}, player); skillRoot.AddChild(skills);
			var skillList = skillRoot.GetNode<Control>("bg/lh_pic/front_bg").GetChildren().OfType<Control>().First(node => node.HasNode("ScrollContainer/HBoxContainer"));
			const string skillRows = "ScrollContainer/HBoxContainer";
			var help = skillList.GetNode<Button>($"{skillRows}/sk_pi/ski_1/DetailsHelp");
			Check(help.TooltipText.Contains("魔耗：37") && help.TooltipText.Contains("当前生效等级：4") && !skillList.GetNode<Label>($"{skillRows}/sk_ms/s_1").Text.Contains("魔耗"), "numeric help uses effective level and body remains effect-only");
			skillList.GetNode<Button>($"{skillRows}/sk_lv/Skill_1").EmitSignal(BaseButton.SignalName.Pressed);
			Check(help.TooltipText.Contains("魔耗：54") && help.TooltipText.Contains("当前生效等级：5"), "upgrade refreshes numeric help");
			await Capture("active-growth");
			if (_capture)
			{
				Vector2 hoverPosition = help.GetGlobalRect().GetCenter();
				GetViewport().WarpMouse(hoverPosition);
				GetViewport().PushInput(new InputEventMouseMotion { Position = hoverPosition, GlobalPosition = hoverPosition });
				await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
				await Capture("active-values-tooltip");
			}
			skillRoot.GetNode<BaseButton>("bg/bd_skill").EmitSignal(BaseButton.SignalName.Pressed);
			int radianceIndex = learning.Catalog.Skills.ToList().FindIndex(entry => entry.Id == "hh") + 1;
			var passiveHelp = skillList.GetNode<Button>($"{skillRows}/sk_pi/ski_{radianceIndex}/DetailsHelp");
			Check(passiveHelp.TooltipText.Contains("1级预览") && passiveHelp.TooltipText.Contains("+2/秒"), "unlearned passive previews registered level-one regeneration");
			skillList.GetNode<Button>($"{skillRows}/sk_lv/Skill_{radianceIndex}").EmitSignal(BaseButton.SignalName.Pressed);
			Check(passiveHelp.TooltipText.Contains("当前生效等级：3") && passiveHelp.TooltipText.Contains("+4/秒"), "learning passive refreshes values including equipment level bonus");
			await Capture("passive-icons"); skillRoot.Hide();
			var data = new GameSaveData();
			var questCatalog = GD.Load<QuestCatalog>("res://Content/Quests/Registry.tres");
			var questService = new QuestService(questCatalog, character, data, inventory, items);
			using var interaction = new QuestInteraction(questService, () => saves++);
			var questRoot = GD.Load<PackedScene>("res://Scenes/UI/Task/BasicTask.tscn").Instantiate<Node2D>(); AddChild(questRoot);
			var quests = new LegacyQuestPanel(); questRoot.AddChild(quests); quests.Bind(questRoot, questService, interaction, items);
			await Capture("quests");
			questRoot.GetNode<BaseButton>("BG/lqjl").EmitSignal(BaseButton.SignalName.Pressed);
			Check(data.ClaimedQuestIds.Contains("growth_gift_1") && inventory.Slots.Where(stack => stack?.ItemId == "qhsbx").Sum(stack => stack!.Count) == 10, "actual quest button grants original complete reward and records claim");
			Check(!interaction.ClaimRequested.Send(new("growth_gift_1")).Success, "duplicate claim rejected");
			var serializer = new JsonSaveSerializer();
			Check(serializer.Deserialize(serializer.Serialize(InventorySaveMapper.Capture(inventory, data))).ClaimedQuestIds.Contains("growth_gift_1"), "claim survives save serialization");
			var full = new InventoryService(1, items); full.AddItem("ryjgb", 1);
			var blocked = new QuestService(questCatalog, character, new(), full, items);
			Check(blocked.Claim(new("growth_gift_1")).Success && blocked.IsClaimed(questCatalog.Definitions[0])
				&& full.Slots[0]!.ItemId == "ryjgb" && full.Capacity > 1, "quest rewards grow the bag and preserve existing equipment");
			character.Level = 1;
			Check(!blocked.Claim(new("growth_gift_1")).Success, "quest rechecks level condition");
			character.Level = 10;
			var recoveryData = new GameSaveData();
			var recovery = new QuestService(questCatalog, character, recoveryData, new InventoryService(70, items), items);
			using var failingSave = new QuestInteraction(recovery, () => throw new System.IO.IOException("fixture failure"));
			var saveFailure = failingSave.ClaimRequested.Send(new("growth_gift_1"));
			Check(saveFailure.Success && saveFailure.Message.Contains("存档保存失败") && recovery.IsClaimed(questCatalog.Definitions[0]) &&
				!failingSave.ClaimRequested.Send(new("growth_gift_1")).Success, "save failure reports awarded state and prevents duplicate rewards");
			questRoot.Hide();
			data.Characters.Add(character);
			typeof(GameSession).GetProperty("Slot")!.SetValue(null, 0);
			typeof(GameSession).GetProperty("Data")!.SetValue(null, InventorySaveMapper.Capture(inventory, data));
			typeof(GameSession).GetProperty("Inventory")!.SetValue(null, inventory);
			var map = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/Map1.tscn").Instantiate<Node2D>(); AddChild(map);
			map.GetNode<BaseButton>("Task").EmitSignal(BaseButton.SignalName.Pressed);
			Check(map.GetChildren().OfType<MenuManager>().Single().IsMenuOpen, "main map Task button opens quest menu through MenuManager");
			map.GetChildren().OfType<MenuManager>().Single().CloseMenu();
			map.QueueFree();
			GD.Print("CONTENT SYSTEMS SMOKE TEST PASSED");
			view.Dispose();
			foreach (Node child in GetChildren()) child.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private async Task Capture(string name)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!_capture) return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string directory = ProjectSettings.GlobalizePath("res://.godot/content-checks");
		System.IO.Directory.CreateDirectory(directory);
		GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
	}
	private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.001f;
	private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); GD.Print("PASS: " + message); }
}
