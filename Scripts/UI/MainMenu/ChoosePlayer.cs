using System;
using Godot;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Skills;

namespace Zaomeng.UI.MainMenu;

public partial class ChoosePlayer : Node2D
{
	private SkillCatalog _selected = null!;
	public override void _Ready()
	{
		var registry = SkillCatalogRegistry.Default;
		GetNode<Label>("Bg/CanChooseRole").Text = $"当前可选角色：{registry.Catalogs.Count}位";
		foreach (SkillCatalog role in registry.Catalogs)
		{
			var button = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/RoleButton.tscn").Instantiate<Button>();
			button.Name = role.CharacterId;
			button.Icon = role.Portrait;
			button.AddThemeConstantOverride("icon_max_width", 80);
			button.GetNode<Label>("RoleName").Text = role.DisplayName;
			button.Pressed += () => Select(role);
			GetNode<VBoxContainer>("Bg/ScrollContainer/PlayerList").AddChild(button);
		}
		Select(registry.Catalogs[0]);
		GetNode<BaseButton>("Bg/qued").Pressed += Begin;
		GetNode<BaseButton>("Bg/ReturnMainMenu").Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/MainMenu.tscn");
	}
	private void Select(SkillCatalog role)
	{
		_selected = role;
		GetNode<Label>("Bg/Bg/RoleName").Text = role.DisplayName;
		GetNode<Label>("Bg/Bg/Info").Text = role.Description;
		var animator = GetNode<AnimationPlayer>("Bg/SpecialEffectPlayer");
		if (animator.HasAnimation(role.SelectionAnimation)) animator.Play(role.SelectionAnimation);
	}

	private void Begin()
	{
		try
		{
			ItemCatalog catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			GameSession.BeginNewGame(catalog, _selected.CharacterId);
			GetTree().ChangeSceneToFile(GameSession.FirstMap);
		}
		catch (Exception error) { GD.PushError($"创建存档失败：{error.Message}"); }
	}
}
