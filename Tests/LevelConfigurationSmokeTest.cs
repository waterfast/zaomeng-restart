using System;
using Godot;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Save;
using Zaomeng.UI.MainMenu;

/// <summary>只验证关卡资源、两个变体的预览切换和启动选择，不运行完整战斗。</summary>
public partial class LevelConfigurationSmokeTest : Node
{
	public override void _Ready()
	{
		try
		{
			var map = GD.Load<WorldMapDefinition>("res://Content/Maps/Human.tres");
			var entrance = map.LevelEntrances[0];
			Check(entrance.Levels.Count == 2, "花果山入口应有两个关卡");
			LevelDefinition normal = entrance.Levels[0], dark = entrance.Levels[1];
			Check(normal.WaveEncounter?.Waves.Count == 5 && dark.WaveEncounter?.Waves.Count == 5, "两种花果山应有四波和 Boss");
			Check(normal.CommonDropIds[0] != dark.CommonDropIds[0] &&
				normal.WaveEncounter!.Waves[0].MonsterKinds[0] != dark.WaveEncounter!.Waves[0].MonsterKinds[0],
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
			((Button)tabs.GetChild(1)).EmitSignal(BaseButton.SignalName.Pressed);
			Check(panel.GetNode<Label>("ColorRect/TextureRect/Title").Text.Contains("魔化花果山"), "左侧应切换变体");
			GameSession.SelectLevel(dark, 2);
			Check(ReferenceEquals(GameSession.SelectedLevel, dark) && GameSession.SelectedSpawnSpeed == 2, "挑战选择应进入会话");
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
