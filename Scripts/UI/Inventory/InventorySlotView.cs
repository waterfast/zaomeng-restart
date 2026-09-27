using System;
using Godot;

namespace Zaomeng.UI.Inventory;

/// <summary>纯显示组件：不查询背包、不推断图标文件名，也不执行物品操作。</summary>
public partial class InventorySlotView : TextureButton
{
	[Export] public Texture2D? EmptyTexture { get; set; }

	private InventoryDisplayEntry? _entry;
	private Label _countLabel = null!;

	public event Action<int>? Selected;

	public override void _Ready()
	{
		_countLabel = GetNode<Label>("CountLabel");
		Pressed += OnPressed;
		ShowEntry(null, false);
	}

	public void ShowEntry(InventoryDisplayEntry? entry, bool selected)
	{
		_entry = entry;
		TextureNormal = entry?.Definition.Icon ?? EmptyTexture;
		TextureHover = TextureNormal;
		TexturePressed = TextureNormal;
		_countLabel.Text = entry?.Count > 1 ? entry.Count.ToString() : "";
		SelfModulate = selected ? new Color(1.25f, 1.15f, 0.75f) : Colors.White;
		TooltipText = entry?.Definition.DisplayName ?? "";
	}

	private void OnPressed()
	{
		if (_entry is not null)
			Selected?.Invoke(_entry.SlotIndex);
	}
}
