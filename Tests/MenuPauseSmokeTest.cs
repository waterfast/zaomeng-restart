using System;
using System.Linq;
using Godot;
using Zaomeng.Save;
using Zaomeng.UI;

namespace Zaomeng;

/// <summary>检查 C 键开关、关闭按钮和菜单暂停期间的怪物状态。</summary>
public partial class MenuPauseSmokeTest : Node
{
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			var menuManager = GetParent().GetNode<MenuManager>("MenuManager");
			var backpack = GetParent().GetNode<Node2D>("HUD/BackPack");
			var player = GetParent().GetNode<Player>("Player");
			var monster = GetParent().GetNode<Monster>("Monster");
			var character = GameSession.Data!.Characters.FirstOrDefault(entry => entry.Id == "role_1")
				?? GameSession.Data.Characters[0];
			// 独立测试目录中的新存档背包为空；展示检查自己准备物品，不依赖玩家存档内容。
			Check(GameSession.Inventory!.AddItem("ryjgb", 1), "menu test prepares its own display item");

			PressBagAction(menuManager);
			await NextFrame();
			Check(menuManager.IsMenuOpen && backpack.Visible && GetTree().Paused,
				$"C opens the backpack and pauses the scene tree (open={menuManager.IsMenuOpen}, visible={backpack.Visible}, paused={GetTree().Paused})");
			Check(menuManager.CanProcess() && backpack.CanProcess() &&
				!player.CanProcess() && !monster.CanProcess(),
				"menu remains interactive while actors stop processing");
			Check(!GetParent().GetNode<ColorRect>("HUD/Panel").Visible,
				"test HUD is hidden behind the original backpack");
			Check(backpack.GetNode<LineEdit>("background/Zdlnc/name").Text == character.Name,
				"original character panel reads the selected save");
			Check(backpack.GetNode<Label>("background/infomation/att").Text ==
				(character.BaseStats.Attack + character.PermanentBonuses.Attack).ToString("0.##"),
				"attribute row reads character stats");
			backpack.GetNode<TextureButton>("background/infomation/second")
				.EmitSignal(BaseButton.SignalName.Pressed);
			Check(backpack.GetNode<Label>("background/infomation/hp/Hp_tt").Text == "命中",
				"second attribute page uses the original labels");
			Check(backpack.GetNode<Label>("background/infomation/def/Def_tt").Text == "暴免" &&
				backpack.GetNode<Label>("background/infomation/lucky/Lucky_tt").Text == "破甲",
				"second attribute page follows the original row order");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://Tests/legacy-stats-page-preview.png");
			}
			backpack.GetNode<TextureButton>("background/infomation/first")
				.EmitSignal(BaseButton.SignalName.Pressed);
			var grid = backpack.GetNode<GridContainer>(
				"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box");
			Button firstSlot = grid.GetNode<Button>("box_1");
			Color backgroundTint = firstSlot.SelfModulate;
			firstSlot.EmitSignal(BaseButton.SignalName.Pressed);
			Check(firstSlot.Icon == GD.Load<Texture2D>("res://Assets/Art/BackPack/AllItems/empty.png") &&
				firstSlot.GetNode<TextureRect>("ItemIcon").Texture is not null,
				"filled slots use the same bright base image as empty slots");
			Check(firstSlot.SelfModulate == backgroundTint &&
				grid.GetNode<Button>("box_2").SelfModulate == backgroundTint,
				"clicking an item does not recolor other slots");
			var itemTooltip = backpack.GetNode<Node2D>("ItemTooltip");
			Check(firstSlot.TooltipText.Length == 0 && !itemTooltip.Visible && backpack.GetNode<PopupPanel>("ItemActionMenu").Visible,
				"single click opens actions and hides the hover tooltip");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://Tests/backpack-item-popup-preview.png");
			}
			firstSlot.EmitSignal(Control.SignalName.MouseExited);
			backpack.GetNode<TextureButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/dj")
				.EmitSignal(BaseButton.SignalName.Pressed);
			Check(backpack.GetNode<Label>("Main_Backpack/MarginContainer/VBoxContainer/title").Text == "道具背包",
				"old category buttons switch the new inventory data");
			backpack.GetNode<TextureButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/zb")
				.EmitSignal(BaseButton.SignalName.Pressed);
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://Tests/legacy-backpack-preview.png");
			}

			Vector2 monsterPosition = monster.Position;
			float monsterHealth = monster.Health;
			for (int i = 0; i < 20; i++)
				await NextFrame();
			Check(monster.Position == monsterPosition && monster.Health == monsterHealth,
				"monster remains still while the menu is open");

			PressBagAction(menuManager);
			await NextFrame();
			Check(!menuManager.IsMenuOpen && !backpack.Visible && !GetTree().Paused,
				"C closes the backpack and resumes gameplay");

			PressBagAction(menuManager);
			await NextFrame();
			backpack.GetNode<TextureButton>("background/close").EmitSignal(BaseButton.SignalName.Pressed);
			await NextFrame();
			Check(!menuManager.IsMenuOpen && !backpack.Visible && !GetTree().Paused,
				"close button resumes gameplay");

			GD.Print("MENU_PAUSE_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"MENU_PAUSE_TEST: FAIL: {error}");
			GetTree().Paused = false;
			GetTree().Quit(1);
		}
	}

	private static void PressBagAction(MenuManager menuManager) => menuManager._Input(new InputEventKey
	{
		PhysicalKeycode = Key.C,
		Keycode = Key.C,
		Pressed = true
	});

	private async System.Threading.Tasks.Task NextFrame() =>
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

	private static void Check(bool condition, string description)
	{
		if (!condition)
			throw new InvalidOperationException(description);
		GD.Print($"PASS: {description}");
	}
}
