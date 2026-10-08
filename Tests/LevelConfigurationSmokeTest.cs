using System;
using System.Linq;
using Godot;
using Zaomeng;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Save;
using Zaomeng.UI.MainMenu;
using Zaomeng.UI.Inventory;

/// <summary>只验证关卡资源、两个变体的预览切换和启动选择，不运行完整战斗。</summary>
public partial class LevelConfigurationSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var map = GD.Load<WorldMapDefinition>("res://Content/Maps/Human.tres");
			var entrance = map.LevelEntrances[0];
			Check(entrance.Levels.Count == 2, "花果山入口应有两个关卡");
			LevelDefinition normal = entrance.Levels[0], dark = entrance.Levels[1];
			Check(normal.WaveEncounter?.Waves.Count == 5 && dark.WaveEncounter?.Waves.Count == 5, "两种花果山应有四波和 Boss");
			Check(normal.CommonDropIds[0] != dark.CommonDropIds[0] &&
				normal.WaveEncounter!.Waves[0].Monsters[0] != dark.WaveEncounter!.Waves[0].Monsters[0],
				"魔化变体应使用不同怪物和掉落");
			Check(normal.MapScene is not null && dark.MapScene is not null, "关卡须引用地图");
			foreach (LevelEntranceDefinition entry in map.LevelEntrances)
				foreach (LevelDefinition level in entry.Levels)
				{
					Check(level.MapScene?.CanInstantiate() == true, $"{level.Id} 的地图无法加载");
					Check(GD.Load<PackedScene>(level.LevelScenePath)?.CanInstantiate() == true, $"{level.Id} 的关卡容器无法加载");
				}
			var panel = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/LevelInfo.tscn").Instantiate<LevelPreviewPanel>();
			panel.Bind(entrance, GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres"));
			AddChild(panel);
			Check(panel.GetNode<Label>("ColorRect/TextureRect/Title").Text.Contains("花果山"), "默认显示普通花果山");
			var root = panel.GetNode("ColorRect");
			var tabs = root.GetChild<VBoxContainer>(root.GetChildCount() - 1);
			((TextureButton)tabs.GetChild(1)).EmitSignal(BaseButton.SignalName.Pressed);
			Check(panel.GetNode<Label>("ColorRect/TextureRect/Title").Text.Contains("魔化花果山"), "左侧应切换变体");
			var difficultyButtons = panel.GetNode<HBoxContainer>("ColorRect/TextureRect/DifficultyChoices");
			((TextureButton)difficultyButtons.GetChild(2)).EmitSignal(BaseButton.SignalName.Pressed);
			LevelDifficultyDefinition? challengedDifficulty = null;
			panel.ChallengeRequested += (_, _, difficulty) => challengedDifficulty = difficulty;
			panel.GetNode<TextureButton>("ColorRect/TextureRect/Challenge").EmitSignal(BaseButton.SignalName.Pressed);
			Check(ReferenceEquals(challengedDifficulty, dark.Difficulties[1]), "挑战应传递独立的难度选择");
			GameSession.SelectLevel(dark, 2, dark.Difficulties[1]);
			Check(ReferenceEquals(GameSession.SelectedLevel, dark) && GameSession.SelectedSpawnSpeed == 2, "挑战选择应进入会话");
			// 用内存会话检查地图按钮的真实接线，槽位 0 不写玩家存档。
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			var inventory = new InventoryService(70, catalog);
			var save = InventorySaveMapper.Capture(inventory);
			var hero = new Character { Id = "role_1", Name = "悟空" };
			CharacterProgression.SyncBaseStats(hero);
			save.Characters.Add(hero);
			save.CurrentCharacterId = hero.Id;
			typeof(GameSession).GetProperty("Slot")!.SetValue(null, 0);
			typeof(GameSession).GetProperty("Data")!.SetValue(null, save);
			typeof(GameSession).GetProperty("Inventory")!.SetValue(null, inventory);
			var world = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/Map1.tscn").Instantiate<WorldMap>();
			AddChild(world);
			var button = world.GetNode<TextureButton>("level_1");
			Check(!button.Disabled, "花果山入口应可点击");
			button.EmitSignal(BaseButton.SignalName.Pressed);
			Check(world.GetChildren().OfType<CanvasLayer>().SelectMany(layer => layer.GetChildren())
				.OfType<LevelPreviewPanel>().Any(), "点击花果山应打开关卡信息面板");
			var preview = world.GetChildren().OfType<CanvasLayer>().SelectMany(layer => layer.GetChildren())
				.OfType<LevelPreviewPanel>().Single();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var drops = preview.GetNode<HBoxContainer>("ColorRect/TextureRect/ScrollContainer/FallList");
			drops.GetChild<Control>(0).EmitSignal(Control.SignalName.MouseEntered);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var tooltip = preview.GetNode<Node2D>("ItemTooltip");
			Check(tooltip.Visible && tooltip.GetNode<Label>("pro_wk/information/inf/VBoxContainer2/eq_lx").Text.Contains("武器"),
				"掉落悬停应展示背包的物品类型，而非仅名称");
			Check(!tooltip.GetNode<Label>("pro_wk/information/inf/VBoxContainer2/have").Visible,
				"可能掉落不能显示虚构的拥有数量");
			if (OS.GetCmdlineUserArgs().Contains("--capture-level-preview"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/level-preview.png");
			}
			// 用内存中的展示格子制造溢出，不修改关卡策划掉落。
			for (int index = 0; index < 15; index++)
				drops.AddChild(new TextureRect { CustomMinimumSize = new(53, 53) });
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var scroll = drops.GetParent<ScrollContainer>();
			var drag = preview.GetChildren().OfType<HorizontalPreviewScroll>().Single();
			Vector2 mouse = scroll.GetGlobalTransformWithCanvas() * new Vector2(100, 20);
			drag._Input(new InputEventMouseButton { Position = mouse, ButtonIndex = MouseButton.WheelDown, Pressed = true });
			Check(scroll.ScrollHorizontal > 0 && !tooltip.Visible, "滚轮应横向移动并隐藏悬停说明");
			int start = scroll.ScrollHorizontal;
			drag._Input(new InputEventMouseButton { Position = mouse, ButtonIndex = MouseButton.Left, Pressed = true });
			drag._Input(new InputEventMouseMotion { Position = mouse - new Vector2(40, 0) });
			drag._Input(new InputEventMouseButton { Position = mouse, ButtonIndex = MouseButton.Left, Pressed = false });
			Check(scroll.ScrollHorizontal > start, "按住物品区域拖动应横向移动");
			var enemies = new Node2D();
			AddChild(enemies);
			var pool = new MonsterPool(enemies, dark.Difficulties[1]);
			Monster first = pool.Spawn(normal.WaveEncounter!.Waves[0].Monsters[0], Vector2.Zero);
			float health = first.MaxHealth, attack = first.Attack;
			var original = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			Check(Mathf.IsEqualApprox(health, normal.WaveEncounter!.Waves[0].Monsters[0].Health * dark.Difficulties[1].MonsterHealthMultiplier)
				&& Mathf.IsEqualApprox(attack, normal.WaveEncounter!.Waves[0].Monsters[0].Attack * dark.Difficulties[1].MonsterAttackMultiplier), "怪物强度应读取难度资源");
			original.Free();
			pool.Release(normal.WaveEncounter!.Waves[0].Monsters[0], first);
			Monster reused = pool.Spawn(normal.WaveEncounter!.Waves[0].Monsters[0], Vector2.Zero);
			Check(reused.MaxHealth == health && reused.Health == health && reused.Attack == attack, "对象池复用不累计难度倍率");
			GD.Print("LevelConfigurationSmokeTest PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"LevelConfigurationSmokeTest FAIL: {error}");
			GetTree().Quit(1);
		}
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
