using System;
using Godot;
using Zaomeng.Save;

namespace Zaomeng.UI.MainMenu;

public partial class SaveSlots : Node2D
{
	private GridContainer _grid = null!;
	private Label _message = null!;
	private LineEdit _deleteNumber = null!;
	private ConfirmationDialog _deleteConfirmation = null!;
	private int _pendingDeleteSlot;

	public override void _Ready()
	{
		_grid = GetNode<GridContainer>("ScrollContainer/AllAr");
		GetNode<BaseButton>("background/close").Pressed += QueueFree;
		GetNode<Button>("background/Addcd").Pressed += AddSlot;
		GetNode<Button>("background/Removecd").Pressed += RemoveSlot;
		GetNode<Button>("background/delete").Pressed += RequestDelete;
		_deleteNumber = GetNode<LineEdit>("background/cd_number");
		_deleteNumber.TextSubmitted += _ => RequestDelete();
		_deleteConfirmation = new ConfirmationDialog { Title = "删除存档" };
		_deleteConfirmation.Confirmed += ConfirmDelete;
		AddChild(_deleteConfirmation);
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
			string text = exists
				? $"存档 {slot:D2} · {GameSession.GetSlotSummary(slot)}"
				: $"存档 {slot:D2} · 新游戏";
			var button = new Button { Text = text, CustomMinimumSize = new Vector2(245, 64) };
			button.Pressed += () => SelectSlot(selectedSlot);
			_grid.AddChild(button);
		}
	}

	private void RequestDelete()
	{
		if (!int.TryParse(_deleteNumber.Text.Trim(), out int slot) || slot is < 1 or > 99)
		{
			_message.Text = "请输入 1～99 的存档号。";
			return;
		}
		if (!GameSession.SlotExists(slot))
		{
			_message.Text = $"存档 {slot:D2} 不存在。";
			return;
		}
		_pendingDeleteSlot = slot;
		_deleteConfirmation.DialogText = $"确定删除存档 {slot:D2}？\n主文件、备份和加密副本都会删除，无法恢复。";
		_deleteConfirmation.PopupCentered();
	}

	private void ConfirmDelete()
	{
		try
		{
			int slot = _pendingDeleteSlot;
			if (!GameSession.DeleteSlot(slot))
				throw new InvalidOperationException("存档文件不存在。");
			_deleteNumber.Text = "";
			Refresh();
			_message.Text = $"已删除存档 {slot:D2}。";
		}
		catch (Exception error) { _message.Text = $"删除失败：{error.Message}"; }
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
			GetTree().ChangeSceneToFile(GameSession.FirstMap);
		}
		catch (Exception error)
		{
			_message.Text = $"读取失败：{error.Message}";
		}
	}
}
