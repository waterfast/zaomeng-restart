using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

/// <summary>只管理分类、分页、选中状态和显示。物品增减由外部控制器决定。</summary>
public partial class BackpackView : Control
{
	private const int SlotsPerPage = 35;
	private static readonly ItemCategory[] Categories =
		[ItemCategory.Equipment, ItemCategory.Material, ItemCategory.Consumable];

	private readonly List<InventorySlotView> _slotViews = new();
	private InventoryViewAdapter? _adapter;
	private ItemCategory _category = ItemCategory.Equipment;
	private int _page;
	private int? _selectedSlotIndex;
	private GridContainer _grid = null!;
	private ScrollContainer _scroll = null!;
	private Label _title = null!;
	private Label _pageLabel = null!;
	private Label _itemName = null!;
	private Label _itemStats = null!;
	private Label _itemDescription = null!;
	private ItemDescriptionBinding _description = null!;
	private TextureRect _detailIcon = null!;
	private TextureButton _previousPage = null!;
	private TextureButton _nextPage = null!;
	private TextureButton[] _tabs = null!;
	private Texture2D _tabNormal = null!;
	private Texture2D _tabSelected = null!;

	public event Action<int>? ItemSelected;
	public event Action? CloseRequested;

	public int? SelectedSlotIndex => _selectedSlotIndex;

	public override void _Ready()
	{
		_grid = GetNode<GridContainer>("InventoryPanel/Scroll/Grid");
		_scroll = GetNode<ScrollContainer>("InventoryPanel/Scroll");
		_title = GetNode<Label>("InventoryPanel/Title");
		_pageLabel = GetNode<Label>("InventoryPanel/PageLabel");
		_itemName = GetNode<Label>("Detail/ItemName");
		_itemStats = GetNode<Label>("Detail/ItemStats");
		_itemDescription = GetNode<Label>("Detail/Description");
		_description = ItemDescriptionBinding.Attach(_itemDescription);
		_detailIcon = GetNode<TextureRect>("Detail/Icon");
		_previousPage = GetNode<TextureButton>("InventoryPanel/PreviousPage");
		_nextPage = GetNode<TextureButton>("InventoryPanel/NextPage");
		_tabs =
		[
			GetNode<TextureButton>("InventoryPanel/Tabs/Equipment"),
			GetNode<TextureButton>("InventoryPanel/Tabs/Material"),
			GetNode<TextureButton>("InventoryPanel/Tabs/Consumable")
		];
		_tabNormal = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button.png");
		_tabSelected = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button_choose.png");

		PackedScene slotScene = GD.Load<PackedScene>("res://Scenes/UI/BackPack/InventorySlotView.tscn");
		for (int i = 0; i < SlotsPerPage; i++)
		{
			InventorySlotView slot = slotScene.Instantiate<InventorySlotView>();
			slot.Selected += OnSlotSelected;
			_grid.AddChild(slot);
			_slotViews.Add(slot);
		}
		for (int index = 0; index < _tabs.Length; index++)
		{
			int categoryIndex = index;
			_tabs[index].Pressed += () => SelectCategory(Categories[categoryIndex]);
		}
		_previousPage.Pressed += () => ChangePage(-1);
		_nextPage.Pressed += () => ChangePage(1);
		GetNode<TextureButton>("CloseButton").Pressed += () => CloseRequested?.Invoke();
		Refresh();
	}

	public void Bind(InventoryViewAdapter adapter)
	{
		if (_adapter is not null)
			_adapter.Changed -= Refresh;
		_adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
		_adapter.Changed += Refresh;
		if (IsNodeReady())
			Refresh();
	}

	public override void _ExitTree()
	{
		if (_adapter is not null)
			_adapter.Changed -= Refresh;
	}

	private void SelectCategory(ItemCategory category)
	{
		_category = category;
		_page = 0;
		_selectedSlotIndex = null;
		_scroll.ScrollVertical = 0;
		Refresh();
	}

	private void ChangePage(int direction)
	{
		_page += direction;
		_selectedSlotIndex = null;
		_scroll.ScrollVertical = 0;
		Refresh();
	}

	private void OnSlotSelected(int slotIndex)
	{
		_selectedSlotIndex = slotIndex;
		Refresh();
		ItemSelected?.Invoke(slotIndex);
	}

	private void Refresh()
	{
		if (!IsNodeReady())
			return;

		IReadOnlyList<InventoryDisplayEntry> entries =
			_adapter?.GetEntries(_category) ?? Array.Empty<InventoryDisplayEntry>();
		int pageCount = Math.Max(1, (entries.Count + SlotsPerPage - 1) / SlotsPerPage);
		_page = Math.Clamp(_page, 0, pageCount - 1);
		if (_selectedSlotIndex is null && _page * SlotsPerPage < entries.Count)
			_selectedSlotIndex = entries[_page * SlotsPerPage].SlotIndex;
		_pageLabel.Text = $"{_page + 1}/{pageCount}";
		_previousPage.Disabled = _page == 0;
		_nextPage.Disabled = _page == pageCount - 1;
		_title.Text = _category switch
		{
			ItemCategory.Equipment => "装备背包",
			ItemCategory.Material => "道具背包",
			_ => "消耗品背包"
		};
		for (int i = 0; i < _tabs.Length; i++)
		{
			bool active = Categories[i] == _category;
			_tabs[i].TextureNormal = active ? _tabSelected : _tabNormal;
		}

		InventoryDisplayEntry? selectedEntry = null;
		for (int i = 0; i < _slotViews.Count; i++)
		{
			int entryIndex = _page * SlotsPerPage + i;
			InventoryDisplayEntry? entry = entryIndex < entries.Count ? entries[entryIndex] : null;
			bool selected = entry is not null && entry.SlotIndex == _selectedSlotIndex;
			_slotViews[i].ShowEntry(entry, selected);
			if (selected)
				selectedEntry = entry;
		}
		if (selectedEntry is null)
			_selectedSlotIndex = null;
		ShowDetails(selectedEntry);
	}

	private void ShowDetails(InventoryDisplayEntry? entry)
	{
		if (entry is null)
		{
			_itemName.Text = "选择一件物品";
			_itemStats.Text = "";
			_description.Show(null);
			_detailIcon.Texture = null;
			return;
		}

		ItemDefinition definition = entry.Definition;
		_itemName.Text = definition.DisplayName;
		_itemStats.Text = definition.Category == ItemCategory.Equipment
			? $"攻击 +{definition.Attack}\n暴击值 +{definition.CriticalRating}\n命中值 +{definition.Accuracy}"
			: $"数量 {entry.Count}";
		_description.Show(definition);
		_detailIcon.Texture = definition.Icon;
	}
}
