using System;
using System.Linq;
using Godot;
using Zaomeng.Save;
using Zaomeng.UI;

namespace Zaomeng.Level;

/// <summary>核对主菜单进入的关卡实际挂载了旧背包和当前角色数据。</summary>
public partial class GameplayBackpackSmokeTest : Node
{
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			Node level = GetParent();
			MenuManager menus = level.GetNode<MenuManager>("MenuManager");
			Node2D backpack = level.GetNode<Node2D>("HUD/BackPack");
			var character = GameSession.Data!.Characters.FirstOrDefault(entry => entry.Id == "role_1")
				?? GameSession.Data.Characters[0];
			menus.ToggleMenu("bag");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(backpack.Visible && menus.IsMenuOpen && GetTree().Paused, "level opens the original backpack and pauses");
			Check(!level.GetNode<CanvasLayer>("OldHud/roleLayer").Visible,
				"level HUD is hidden behind the backpack");
			Check(backpack.GetNode<LineEdit>("background/Zdlnc/name").Text == character.Name,
				"level backpack reads the current save character");
			Check(backpack.GetNode<Label>("background/infomation/hp/Hp_tt").Text ==
				TranslationServer.Translate("生命").ToString(), "stat caption uses translation data");
			if (TranslationServer.GetLocale().StartsWith("en", StringComparison.Ordinal))
				Check(backpack.GetNode<Label>("background/infomation/hp/Hp_tt").Text == "Health",
					"English stat caption is loaded from the CSV");
			backpack.GetNode<TextureButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/dj")
				.EmitSignal(BaseButton.SignalName.Pressed);
			Check(backpack.GetNode<Label>("Main_Backpack/MarginContainer/VBoxContainer/title").Text ==
				TranslationServer.Translate("道具背包").ToString(), "category uses translation data");
			if (TranslationServer.GetLocale().StartsWith("en", StringComparison.Ordinal))
				Check(backpack.GetNode<Label>("Main_Backpack/MarginContainer/VBoxContainer/title").Text == "Item Bag",
					"English category title is loaded from the CSV");
			backpack.GetNode<TextureButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/zb")
				.EmitSignal(BaseButton.SignalName.Pressed);
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://Tests/gameplay-backpack-preview.png");
			}
			menus.CloseMenu();
			Check(!backpack.Visible && !GetTree().Paused, "level closes the backpack and resumes");
			Check(level.GetNode<CanvasLayer>("OldHud/roleLayer").Visible,
				"level HUD returns after closing");
			GD.Print("GAMEPLAY_BACKPACK_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"GAMEPLAY_BACKPACK_TEST: FAIL: {error}");
			GetTree().Paused = false;
			GetTree().Quit(1);
		}
	}

	private static void Check(bool condition, string description)
	{
		if (!condition) throw new InvalidOperationException(description);
		GD.Print($"PASS: {description}");
	}
}
