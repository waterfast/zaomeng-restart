using Godot;

namespace Zaomeng.UI.MainMenu;

public partial class MainMenu : Node2D
{
	private const string SlotsScene = "res://Scenes/UI/MainMenu/SaveSlots.tscn";

	public override void _Ready()
	{
		GetNode<Button>("ButtonList/Begin_game").Pressed += OpenSaves;
		// 旧菜单的其他入口还没有对应的新系统，保留其美术但避免误导点击。
		foreach (Node child in GetNode("ButtonList").GetChildren())
			if (child is Button button && button.Name != "Begin_game") button.Disabled = true;
	}

	private void OpenSaves()
	{
		Node host = GetNode("ar_infer");
		if (host.GetChildCount() == 0)
			host.AddChild(GD.Load<PackedScene>(SlotsScene).Instantiate());
	}
}
