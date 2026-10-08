using Godot;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

/// <summary>复用旧背包物块；只显示与浏览，保留透明按钮和数量层。</summary>
public sealed class LegacyBrowseSlot
{
	public Button Button { get; }
	private readonly TextureRect _icon;
	private readonly Label _count;
	private readonly LegacyItemTooltip _tooltip;
	private readonly ItemCatalog? _catalog;
	private InventoryDisplayEntry? _entry;

	public LegacyBrowseSlot(Button button, LegacyItemTooltip tooltip, ItemCatalog? catalog = null)
	{
		Button = button;
		_tooltip = tooltip;
		_catalog = catalog;
		button.Icon = GD.Load<Texture2D>("res://Assets/Art/BackPack/AllItems/empty.png");
		button.Flat = true;
		button.TooltipText = "";
		_count = button.GetNode<Label>("item_number");
		_count.MouseFilter = Control.MouseFilterEnum.Ignore;
		_icon = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
		button.AddChild(_icon);
		_icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		button.MoveChild(_icon, 0);
		button.MouseEntered += ShowTooltip;
		button.MouseExited += tooltip.Hide;
		button.Pressed += ShowTooltip;
	}

	public void Show(InventoryDisplayEntry? entry)
	{
		_entry = entry;
		_icon.Texture = entry?.Definition.Icon;
		_count.Text = entry?.Count > 1 ? entry.Count.ToString() : "";
	}

	private void ShowTooltip()
	{
		if (_entry is null) return;
		Rect2 area = Button.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, Button.Size);
		_tooltip.Show(_entry.Definition, _entry.Count, area, _entry.Equipment, _catalog);
	}
}
