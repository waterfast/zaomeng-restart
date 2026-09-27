using System;
using Godot;
using Zaomeng.Save;

namespace Zaomeng.UI.MainMenu;

public partial class SaveSlots : Node2D
{
	private GridContainer _grid = null!;
	private Label _message = null!;

	public override void _Ready()
	{
		_grid = GetNode<GridContainer>("ScrollContainer/AllAr");
		GetNode<BaseButton>("background/close").Pressed += QueueFree;
		GetNode<Button>("background/Addcd").Pressed += AddSlot;
		GetNode<Button>("background/Removecd").Pressed += RemoveSlot;
		GetNode<Button>("background/delete").Disabled = true;
		_message = new Label { Position = new Vector2(214, 486) };
		AddChild(_message);
		Refresh();
	}

	private int VisibleSlots { get; set; } = 6;
	private void AddSlot() { VisibleSlots = Math.Min(99, VisibleSlots + 1); Refresh(); }
	private void RemoveSlot() { VisibleSlots = Math.Max(1, VisibleSlots - 1); Refresh(); }

	private void Refresh()
	{
		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}
		for (int slot = 1; slot <= VisibleSlots; slot++)
		{
			int selectedSlot = slot;
			bool exists = GameSession.SlotExists(slot);
			var button = new Button { Text = exists ? $"存档 {slot:D2} · 进入关卡" : $"存档 {slot:D2} · 新游戏", CustomMinimumSize = new Vector2(245, 64) };
			button.Pressed += () => SelectSlot(selectedSlot);
			_grid.AddChild(button);
		}
	}

	private void SelectSlot(int slot)
	{
		if (!GameSession.SlotExists(slot))
		{
			GameSession.SelectNewSlot(slot);
			GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/ChoosePlayer.tscn");
			return;
		}
		try
		{
			GameSession.Load(slot);
			GetTree().ChangeSceneToFile(GameSession.FirstLevel);
		}
		catch (Exception error)
		{
			_message.Text = $"读取失败：{error.Message}";
		}
	}
}
