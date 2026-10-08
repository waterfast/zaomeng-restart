using System;
using System.Linq;
using Godot;
using Zaomeng;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Save;
using Zaomeng.Settings;
using Zaomeng.UI.MainMenu;

/// <summary>模式强度、预览与真实击杀奖励；使用槽位0和内存测试配置。</summary>
public partial class LevelModesSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			DirAccess.RemoveAbsolute("user://level_modes_smoke_settings.cfg");
			GameSettings.Load("user://level_modes_smoke_settings.cfg");
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			var forest = GD.Load<LevelDefinition>("res://Content/Levels/forest.tres");
			var dark = GD.Load<LevelDefinition>("res://Content/Levels/forest_dark.tres");
			forest.Validate(catalog);
			dark.Validate(catalog);
			var hard = forest.Difficulties[1];
			Check(hard != dark.Difficulties[1], "两关困难资源独立，可分别调整数值和奖励");
			Check(!forest.GetPossibleDropIds(forest.Difficulties[0]).Contains("qxsh")
				&& !forest.GetPossibleDropIds(hard).Contains("qxsh") && hard.ExtraMonsterDrops is null && hard.ExtraBossDrops is null, "早期困难不提前发放后期装备");
			var boss = forest.WaveEncounter!.Waves.SelectMany(w => w.Monsters).First(m => m.IsBoss);
			var monkey = forest.WaveEncounter.Waves[0].Monsters[0];
			var actor = monkey.ActorScene.Instantiate<Monster>();
			monkey.Apply(actor, forest.Difficulties[0]);
			Check(actor.MaxHealth == monkey.Health && actor.Attack == monkey.Attack && actor.Level == monkey.Level,
				"普通模式沿用怪物原始数值");
			monkey.Apply(actor, hard);
			Check(actor.Level == 20 && actor.MaxHealth == 300 && actor.Attack == 50 && actor.PhysicalDefense == 62.5f,
				"困难小猴按20级试玩强度配置");
			boss.Apply(actor, hard);
			Check(actor.Level == 20 && actor.MaxHealth == 3000 && actor.Attack == 195 && actor.PhysicalDefense == 75,
				"Boss使用独立强度配置");
			actor.Free();
			var random = new RandomNumberGenerator { Seed = 42 };
			var sampleDrops = new MonsterDropTable { Entries = new() { new MonsterDropEntry { ItemId = "jcsz", Probability = 0.1f } } };
			int successful = Enumerable.Range(0, 10000).Count(_ => sampleDrops.Roll(random).Any());
			Check(successful is > 800 and < 1200, "模式额外小怪掉落按10%独立判定");
			var entrance = new LevelEntranceDefinition { Levels = new() { forest } };
			var panel = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/LevelInfo.tscn").Instantiate<LevelPreviewPanel>();
			panel.Bind(entrance, catalog);
			AddChild(panel);
			var dropRow = panel.GetNode("ColorRect/TextureRect/ScrollContainer/FallList");
			Check(new[] { "radish", "Bble", "peach" }.All(id => !dropRow.HasNode(id))
				&& forest.GetPossibleDropIds(forest.Difficulties[0]).Contains("radish"), "补给实际掉落保留，选择面板不显示消耗品");
			var choices = panel.GetNode<HBoxContainer>("ColorRect/TextureRect/DifficultyChoices");
			choices.GetChild<TextureButton>(2).EmitSignal(BaseButton.SignalName.Pressed);
			Check(panel.GetNode<Label>("ColorRect/TextureRect/Level/LevelDown").Text == "20"
				&& !panel.GetNode("ColorRect/TextureRect/ScrollContainer/FallList").HasNode("qxsh"),
				"切换困难同步建议等级且不展示虚构装备");
			choices.GetChild<TextureButton>(1).EmitSignal(BaseButton.SignalName.Pressed);
			Check(panel.GetNode<Label>("ColorRect/TextureRect/Level/LevelDown").Text == "5"
				&& !panel.GetNode("ColorRect/TextureRect/ScrollContainer/FallList").HasNode("qxsh"),
				"切回普通恢复原始预览");
			panel.QueueFree();

			// 只在内存中把额外掉率改为必掉，验证实际击杀发奖链路而不依赖随机运气。
			var fixture = (LevelDefinition)forest.Duplicate(false);
			var mode = (LevelDifficultyDefinition)hard.Duplicate(true);
			mode.ExtraMonsterDrops = new MonsterDropTable { Entries = new() { new MonsterDropEntry { ItemId = "jcsz", Probability = 1 } } };
			mode.ExtraBossDrops = new MonsterDropTable { Entries = new() { new MonsterDropEntry { ItemId = "qxsh", Probability = 1 } } };
			fixture.Difficulties = new() { mode };
			var invalidMode = (LevelDifficultyDefinition)mode.Duplicate(true);
			invalidMode.ExtraBossDrops!.Entries[0].ItemId = "unregistered_mode_drop";
			fixture.Difficulties = new() { invalidMode };
			bool rejected = false;
			try { fixture.Validate(catalog); } catch (InvalidOperationException) { rejected = true; }
			Check(rejected, "未登记的模式额外掉落在入关前被拒绝");
			fixture.Difficulties = new() { mode };
			var inventory = new InventoryService(100, catalog);
			var hero = new Zaomeng.Character.Character { Id = "role_1", Name = "模式测试", Level = 20 };
			CharacterProgression.SyncBaseStats(hero);
			var save = InventorySaveMapper.Capture(inventory);
			save.Characters.Add(hero);
			save.CurrentCharacterId = hero.Id;
			typeof(GameSession).GetProperty("Slot")!.SetValue(null, 0);
			typeof(GameSession).GetProperty("Data")!.SetValue(null, save);
			typeof(GameSession).GetProperty("Inventory")!.SetValue(null, inventory);
			GameSession.SelectLevel(fixture, 2, mode);
			var level = GD.Load<PackedScene>(forest.LevelScenePath).Instantiate<GameplayLevel>();
			AddChild(level);
			var player = level.GetNode<Player>("Player");
			player.InputEnabled = false;
			player.SetPhysicsProcess(false);
			var encounter = level.GetChildren().OfType<WaveEncounter>().Single();
			var lethal = new HitDefinition { FlatDamage = 100000, DamageType = DamageType.True, CanCrit = false };
			Engine.TimeScale = 20;
			int commonKills = 0, bossKills = 0;
			for (int frame = 0; frame < 1800 && !encounter.IsCleared; frame++)
			{
				player.Position = new(encounter.Wave switch { 1 => 380, 2 => 1950, 3 => 2850, _ => 3800 }, 502);
				foreach (var monster in level.GetNode("Enemies").GetChildren().OfType<Monster>())
				{
					if (!monster.Visible || monster.IsDead) continue;
					if (monster.Level != 20 || (monster.IsBoss && monster.MaxHealth != 3000))
						throw new InvalidOperationException("实际关卡出生未采用20级困难数值。");
					monster.AiEnabled = false;
					if (!CombatResolver.Resolve(player, monster, lethal)) continue;
					if (monster.IsBoss) bossKills++; else commonKills++;
				}
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			}
			Check(encounter.IsCleared && bossKills == 1 && commonKills == 43, "困难实际波次仍能清场进入通关");
			int Count(string id) => inventory.Slots.Where(slot => slot?.ItemId == id).Sum(slot => slot!.Count);
			var pickups = level.GetChildren().OfType<ItemPickup>().ToArray();
			Check(pickups.Count(p => p.Item.Id == "jcsz") == commonKills
				&& pickups.Count(p => p.Item.Id == "qxsh") == bossKills,
				"每次击杀生成对应模式额外掉落实物");
			Check(Count("jcsz") == 0 && Count("qxsh") == 0, "远处击杀不会直接把掉落加入背包");
			Check(pickups.All(p => p.Visible && p.GetNode<Sprite2D>("Visual/Icon").Texture is not null
				&& p.GetNode<Label>("Visual/Name").Text.Length > 0), "地面掉落具有图标和物品名称");
			player.GlobalPosition = new(-1000, -1000);
			for (int frame = 0; frame < 30; frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Check(Count("qxsh") == 0 && !GameSettings.AutomaticPickup, "关闭自动拾取后远处等待仍不入包");

			// 初始格子用满后拾取必须自动扩容，不再阻止玩家领取。
			Check(inventory.AddItem("ptxzg", inventory.Capacity), "填满测试背包");
			var blocked = pickups.First(p => p.Item.Id == "qxsh");
			int originalCapacity = inventory.Capacity;
			player.GlobalPosition = blocked.GlobalPosition;
			for (int frame = 0; frame < 10; frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Check(Count("qxsh") == 1 && inventory.Capacity > originalCapacity,
				"用满初始格子后拾取自动扩容");
			if (System.Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--capture-drops"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				DirAccess.MakeDirRecursiveAbsolute("res://.godot/drop-checks");
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/drop-checks/ground-drops.png");
			}
			foreach (var pickup in pickups)
			{
				for (int frame = 0; frame < 120 && GodotObject.IsInstanceValid(pickup) && !pickup.Collected; frame++)
				{
					player.GlobalPosition = pickup.GlobalPosition;
					await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				}
				if (GodotObject.IsInstanceValid(pickup) && !pickup.Collected)
					throw new InvalidOperationException("走近地面掉落未能拾取。");
			}
			Check(true, "所有地面掉落走近后可拾取");
			Check(Count("jcsz") == commonKills && Count("qxsh") == bossKills,
				"拾取只领取一次对应模式额外奖励");
			Check(Count("dsyj") == 0 && Count("dslj") == 0 && Count("dshl") == 0,
				"内存额外奖励样例不产生已撤销的Boss装备");
			level.QueueFree();
			Engine.TimeScale = 1;
			GD.Print("LevelModesSmokeTest PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			Engine.TimeScale = 1;
			GD.PushError($"LevelModesSmokeTest FAIL: {error}");
			GetTree().Quit(1);
		}
		finally { GameSettings.Load(); }
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
