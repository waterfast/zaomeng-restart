using System;
using Godot;
using Zaomeng.Save;
using Zaomeng.Settings;

namespace Zaomeng.UI;

/// <summary>暂停菜单保留继续和返回操作，详细偏好共用主菜单设置。</summary>
public partial class LegacySettingsPanel : Node
{
	public void Bind(Control root, MenuManager menus, Action save)
	{
		root.GetNode<BaseButton>("bg/close").Pressed += menus.CloseMenu;
		root.GetNode<BaseButton>("bg/box/continue_game").Pressed += menus.CloseMenu;
		root.GetNode<BaseButton>("bg/box/continue_game2").Pressed += () => Return(GameSession.FirstMap);
		root.GetNode<BaseButton>("bg/box/continue_game4").Pressed += () => Return("res://Scenes/UI/MainMenu/MainMenu.tscn");
		var settings = root.GetNode<Button>("bg/box/BGMControl");
		settings.Text = "音乐 / 游戏设置";
		var effects = root.GetNode<Button>("bg/box/RoleOrMonsterControl");
		void Refresh() => effects.Text = GameSettings.IsEnabled(GameOption.Effects) ? "关闭音效" : "打开音效";
		effects.Pressed += () => { GameSettings.SetEnabled(GameOption.Effects, !GameSettings.IsEnabled(GameOption.Effects)); Refresh(); };
		Refresh();
		settings.Pressed += () =>
		{
			if (root.HasNode("FullSettingsLayer")) return;
			// 完整设置接管 Escape，关掉后再恢复暂停菜单的快捷键。
			menus.SetProcessInput(false);
			var layer = new CanvasLayer { Name = "FullSettingsLayer", Layer = 30, ProcessMode = ProcessModeEnum.Always };
			root.AddChild(layer);
			var blocker = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
			layer.AddChild(blocker);
			blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			var panel = GD.Load<PackedScene>("res://Scenes/UI/Settings/GameSet.tscn").Instantiate<Node2D>();
			panel.TreeExited += () => { layer.QueueFree(); menus.SetProcessInput(true); Refresh(); };
			layer.AddChild(panel);
		};
		void Return(string scene)
		{
			save();
			menus.CloseMenu();
			GetTree().ChangeSceneToFile(scene);
		}
	}
}
