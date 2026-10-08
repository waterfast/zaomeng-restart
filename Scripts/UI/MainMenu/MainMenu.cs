using Godot;

namespace Zaomeng.UI.MainMenu;

public partial class MainMenu : Node2D
{
	private const string SlotsScene = "res://Scenes/UI/MainMenu/SaveSlots.tscn";

	public override void _Ready()
	{
		Zaomeng.Audio.AudioManager.Instance?.EnterMenu();
		GetNode<Button>("ButtonList/Begin_game").Pressed += OpenSaves;
		GetNode<Button>("ButtonList/GameSet").Pressed += OpenSettings;
		// 旧菜单的其他入口还没有对应的新系统，保留其美术但避免误导点击。
		foreach (Node child in GetNode("ButtonList").GetChildren())
			if (child is Button button && button.Name != "Begin_game" && button.Name != "GameSet") button.Disabled = true;
	}

	private void OpenSettings()
	{
		if (HasNode("SettingsLayer")) return;
		var layer = new CanvasLayer { Name = "SettingsLayer", Layer = 20 };
		AddChild(layer);
		var blocker = new ColorRect { Name = "Blocker", Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
		layer.AddChild(blocker);
		blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var panel = GD.Load<PackedScene>("res://Scenes/UI/Settings/GameSet.tscn").Instantiate<Node2D>();
		panel.TreeExited += layer.QueueFree;
		layer.AddChild(panel);
	}

	private void OpenSaves()
	{
		Node host = GetNode("ar_infer");
		if (host.GetChildCount() == 0)
			host.AddChild(GD.Load<PackedScene>(SlotsScene).Instantiate());
	}
}
