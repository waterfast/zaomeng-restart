using System;
using Godot;
using Zaomeng.Items;
using Zaomeng.Save;

namespace Zaomeng.UI.MainMenu;

public partial class ChoosePlayer : Node2D
{
	public override void _Ready()
	{
		GetNode<Label>("Bg/Bg/RoleName").Text = "孙悟空";
		GetNode<Label>("Bg/Bg/Info").Text = "齐天大圣孙悟空，善用棍法，灵活多变。";
		GetNode<Label>("Bg/CanChooseRole").Text = "当前可选角色：孙悟空";
		var roleButton = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/RoleButton.tscn").Instantiate<Button>();
		GetNode<VBoxContainer>("Bg/ScrollContainer/PlayerList").AddChild(roleButton);
		GetNode<AnimationPlayer>("Bg/SpecialEffectPlayer").Play("Role_1");
		GetNode<BaseButton>("Bg/qued").Pressed += Begin;
		GetNode<BaseButton>("Bg/ReturnMainMenu").Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/MainMenu.tscn");
	}

	private void Begin()
	{
		try
		{
			ItemCatalog catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			GameSession.BeginNewGame(catalog);
			GetTree().ChangeSceneToFile(GameSession.FirstMap);
		}
		catch (Exception error) { GD.PushError($"创建存档失败：{error.Message}"); }
	}
}
