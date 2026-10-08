using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Items;
using Zaomeng.Save;

namespace Zaomeng.UI.Inventory;

/// <summary>独立旧背包的浏览绑定，不接出售、穿戴或加工命令。</summary>
public sealed class LegacyBrowseInventory : IDisposable
{
	private const int PageSize = 25;
	private readonly Control _root;
	private readonly InventoryViewAdapter _adapter;
	private readonly Wallet _wallet;
	private readonly LegacyItemTooltip _tooltip;
	private readonly List<LegacyBrowseSlot> _slots = new();
	private readonly TextureButton[] _tabs;
	private ItemCategory _category;
	private int _page;

	public LegacyBrowseInventory(Control root, InventoryViewAdapter adapter, Wallet wallet, ItemCatalog items)
	{
		_root = root;
		_adapter = adapter;
		_wallet = wallet;
		_tooltip = new LegacyItemTooltip(root);
		var grid = root.GetNode<GridContainer>("MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box");
		foreach (Node child in grid.GetChildren())
			if (child is Button button) _slots.Add(new LegacyBrowseSlot(button, _tooltip, items));
		_tabs = [root.GetNode<TextureButton>("MarginContainer/VBoxContainer/HBoxContainer/zb"),
			root.GetNode<TextureButton>("MarginContainer/VBoxContainer/HBoxContainer/dj"),
			root.GetNode<TextureButton>("MarginContainer/VBoxContainer/HBoxContainer/xhp")];
		for (int i = 0; i < _tabs.Length; i++)
		{
			ItemCategory category = (ItemCategory)i;
			_tabs[i].Pressed += () => { _category = category; _page = 0; Refresh(); };
		}
		root.GetNode<CanvasItem>("MarginContainer/VBoxContainer/HBoxContainer/pl_sell").Hide();
		root.GetNode<TextureButton>("ChangePage/LastPage").Pressed += () => { _page--; Refresh(); };
		root.GetNode<TextureButton>("ChangePage/NextPage").Pressed += () => { _page++; Refresh(); };
		_adapter.Changed += Refresh;
		Refresh();
	}

	public void Refresh()
	{
		_tooltip.Hide();
		var entries = _adapter.GetEntries(_category);
		int pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
		_page = Math.Clamp(_page, 0, pages - 1);
		for (int i = 0; i < _slots.Count; i++)
			_slots[i].Show(_page * PageSize + i < entries.Count ? entries[_page * PageSize + i] : null);
		_root.GetNode<Label>("MarginContainer/VBoxContainer/title").Text = _category switch
		{ ItemCategory.Equipment => "装备背包", ItemCategory.Material => "道具背包", _ => "消耗品背包" };
		_root.GetNode<Label>("ChangePage/CurrentPageText").Text = $"{_page + 1}/{pages}";
		_root.GetNode<TextureButton>("ChangePage/LastPage").Disabled = _page == 0;
		_root.GetNode<TextureButton>("ChangePage/NextPage").Disabled = _page == pages - 1;
		_root.GetNode<Label>("coin_text/coin_number").Text = _wallet.Souls.ToString();
		for (int i = 0; i < _tabs.Length; i++)
			_tabs[i].TextureNormal = GD.Load<Texture2D>(i == (int)_category
				? "res://Assets/Art/BackPack/zb_button_choose.png" : "res://Assets/Art/BackPack/zb_button.png");
	}

	public void HideTooltip() => _tooltip.Hide();
	public void Dispose() => _adapter.Changed -= Refresh;
}
