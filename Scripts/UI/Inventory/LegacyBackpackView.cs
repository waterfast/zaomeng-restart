using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Character;
using Zaomeng.Items;
using Zaomeng.Save;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>给搬运的旧背包场景绑定新数据，保留其原节点、贴图和布局。</summary>
public sealed class LegacyBackpackView : IDisposable
{
	private const int SlotsPerPage = 35;
	private static readonly ItemCategory[] Categories =
		[ItemCategory.Equipment, ItemCategory.Material, ItemCategory.Consumable];
	private static readonly string GridPath =
		"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box";
	private static readonly string TabsPath =
		"Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer";

	private readonly Node2D _root;
	private readonly InventoryViewAdapter _adapter;
	private readonly ItemCatalog _catalog;
	private readonly SaveCharacter _character;
	private readonly Wallet _wallet;
	private readonly Action _saveChanges;
	private readonly CharacterStatsPresenter _stats;
	private readonly List<Button> _slots = new();
	private readonly List<TextureRect> _itemIcons = new();
	private readonly Texture2D _emptyIcon;
	private readonly Texture2D _tabNormal;
	private readonly Texture2D _tabSelected;
	private readonly ScrollContainer _scroll;
	private readonly Label _title;
	private readonly Label _pageLabel;
	private readonly Label _coinLabel;
	private readonly LineEdit _name;
	private readonly TextureButton _previousPage;
	private readonly TextureButton _nextPage;
	private readonly TextureButton[] _tabs;
	private readonly Dictionary<string, (Button Button, Texture2D? EmptyIcon)> _equipmentSlots = new();
	private ItemCategory _category = ItemCategory.Equipment;
	private IReadOnlyList<InventoryDisplayEntry> _entries = Array.Empty<InventoryDisplayEntry>();
	private int _page;
	private int? _selectedSlot;

	public event Action? CloseRequested;

	public LegacyBackpackView(Node2D root, InventoryViewAdapter adapter, ItemCatalog catalog,
		SaveCharacter character, Wallet wallet, Player player, Action saveChanges)
	{
		_root = root;
		_adapter = adapter;
		_catalog = catalog;
		_character = character;
		_wallet = wallet;
		_saveChanges = saveChanges;
		_stats = new CharacterStatsPresenter(root, character, player);
		_emptyIcon = GD.Load<Texture2D>("res://Assets/Art/BackPack/AllItems/empty.png");
		_tabNormal = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button.png");
		_tabSelected = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button_choose.png");
		_scroll = root.GetNode<ScrollContainer>(
			"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box");
		_title = root.GetNode<Label>("Main_Backpack/MarginContainer/VBoxContainer/title");
		_pageLabel = root.GetNode<Label>("Main_Backpack/ChangePage/CurrentPageText");
		_coinLabel = root.GetNode<Label>("Main_Backpack/coin_text/coin_number");
		_name = root.GetNode<LineEdit>("background/Zdlnc/name");
		_previousPage = root.GetNode<TextureButton>("Main_Backpack/ChangePage/LastPage");
		_nextPage = root.GetNode<TextureButton>("Main_Backpack/ChangePage/NextPage");
		_tabs =
		[
			root.GetNode<TextureButton>($"{TabsPath}/zb"),
			root.GetNode<TextureButton>($"{TabsPath}/dj"),
			root.GetNode<TextureButton>($"{TabsPath}/xhp")
		];

		GridContainer grid = root.GetNode<GridContainer>(GridPath);
		var cutoutMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://Shaders/BackpackIconCutout.gdshader")
		};
		cutoutMaterial.SetShaderParameter("empty_texture", _emptyIcon);
		foreach (Node child in grid.GetChildren())
		{
			if (child is not Button button)
				continue;
			int index = _slots.Count;
			button.Pressed += () => SelectSlot(index);
			button.Icon = _emptyIcon;
			var itemIcon = new TextureRect
			{
				Name = "ItemIcon",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Material = cutoutMaterial
			};
			button.AddChild(itemIcon);
			itemIcon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			button.MoveChild(itemIcon, 0); // 数量标签始终绘制在物品图案上方。
			_slots.Add(button);
			_itemIcons.Add(itemIcon);
		}
		if (_slots.Count != SlotsPerPage)
			throw new InvalidOperationException($"旧背包应有 {SlotsPerPage} 个格子，实际为 {_slots.Count} 个。");

		for (int i = 0; i < _tabs.Length; i++)
		{
			ItemCategory category = Categories[i];
			_tabs[i].Pressed += () => SelectCategory(category);
		}
		_previousPage.Pressed += () => ChangePage(-1);
		_nextPage.Pressed += () => ChangePage(1);
		root.GetNode<TextureButton>("background/close").Pressed += () => CloseRequested?.Invoke();
		_name.TextSubmitted += _ => CommitName();
		_name.FocusExited += CommitName;
		BindEquipmentSlots();
		AnimationPlayer animation = root.GetNode<AnimationPlayer>("background/Player");
		if (animation.HasAnimation("wait")) animation.Play("wait");
		_adapter.Changed += RefreshItems;
		_root.VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public void Dispose()
	{
		_adapter.Changed -= RefreshItems;
		_root.VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (_root.Visible) Refresh();
	}

	private void Refresh()
	{
		if (!_name.HasFocus()) _name.Text = _character.Name;
		_coinLabel.Text = _wallet.Souls.ToString();
		_stats.Refresh();
		RefreshEquipment();
		RefreshItems();
	}

	private void SelectCategory(ItemCategory category)
	{
		_category = category;
		_page = 0;
		_selectedSlot = null;
		_scroll.ScrollVertical = 0;
		RefreshItems();
	}

	private void ChangePage(int delta)
	{
		_page += delta;
		_selectedSlot = null;
		_scroll.ScrollVertical = 0;
		RefreshItems();
	}

	private void SelectSlot(int index)
	{
		int entryIndex = _page * SlotsPerPage + index;
		if (entryIndex >= _entries.Count) return;
		_selectedSlot = _entries[entryIndex].SlotIndex;
		RefreshItems();
	}

	private void RefreshItems()
	{
		_entries = _adapter.GetEntries(_category);
		int pageCount = Math.Max(1, (_entries.Count + SlotsPerPage - 1) / SlotsPerPage);
		_page = Math.Clamp(_page, 0, pageCount - 1);
		_pageLabel.Text = $"{_page + 1}/{pageCount}";
		_previousPage.Disabled = _page == 0;
		_nextPage.Disabled = _page == pageCount - 1;
		string title = _category switch
		{
			ItemCategory.Equipment => "装备背包",
			ItemCategory.Material => "道具背包",
			_ => "消耗品背包"
		};
		_title.Text = TranslationServer.Translate(title).ToString();
		for (int i = 0; i < _tabs.Length; i++)
			_tabs[i].TextureNormal = Categories[i] == _category ? _tabSelected : _tabNormal;

		for (int i = 0; i < _slots.Count; i++)
		{
			int entryIndex = _page * SlotsPerPage + i;
			InventoryDisplayEntry? entry = entryIndex < _entries.Count ? _entries[entryIndex] : null;
			Button slot = _slots[i];
			_itemIcons[i].Texture = entry?.Definition.Icon;
			slot.GetNode<Label>("item_number").Text = entry?.Count > 1 ? entry.Count.ToString() : "";
			slot.TooltipText = entry is null ? "" : ItemTooltip(entry);
		}
	}

	private void BindEquipmentSlots()
	{
		const string information = "background/infomation";
		AddEquipmentSlot("wq", $"{information}/equ_/HBoxContainer/wq");
		AddEquipmentSlot("fj", $"{information}/equ_/HBoxContainer/fj");
		AddEquipmentSlot("sp", $"{information}/equ_/VBoxContainer/sp");
		AddEquipmentSlot("fb", $"{information}/equ_/VBoxContainer/fb");
		AddEquipmentSlot("tx", $"{information}/tx");
		AddEquipmentSlot("sz", $"{information}/sz");
		AddEquipmentSlot("cb", $"{information}/cb");
	}

	private void AddEquipmentSlot(string key, string path)
	{
		Button button = _root.GetNode<Button>(path);
		_equipmentSlots.Add(key, (button, button.Icon));
	}

	private void RefreshEquipment()
	{
		EquipmentLoadout equipment = _character.Equipment;
		ShowEquipment("wq", equipment.WeaponId);
		ShowEquipment("fj", equipment.ArmorId);
		ShowEquipment("sp", equipment.AccessoryId);
		ShowEquipment("fb", equipment.MagicWeaponId);
		ShowEquipment("tx", equipment.TitleId);
		ShowEquipment("sz", equipment.CostumeId);
		ShowEquipment("cb", equipment.WingId);
	}

	private void ShowEquipment(string key, string itemId)
	{
		(Button button, Texture2D? emptyIcon) = _equipmentSlots[key];
		if (_catalog.TryGetDefinition(itemId, out ItemDefinition? definition))
		{
			button.Icon = definition!.Icon ?? emptyIcon;
			button.TooltipText = definition.DisplayName;
		}
		else
		{
			button.Icon = emptyIcon;
			button.TooltipText = itemId.Length == 0 ? "" : $"尚未迁入物品定义：{itemId}";
		}
	}

	private void CommitName()
	{
		string name = _name.Text.Trim();
		if (name.Length == 0)
		{
			_name.Text = _character.Name;
			return;
		}
		if (name == _character.Name) return;
		_character.Name = name;
		_saveChanges();
	}

	private static string ItemTooltip(InventoryDisplayEntry entry)
	{
		ItemDefinition definition = entry.Definition;
		string stats = definition.Category == ItemCategory.Equipment
			? $"攻击 +{definition.Attack}　暴击 +{definition.CriticalChance}%"
			: $"数量 {entry.Count}";
		return $"{definition.DisplayName}\n{stats}\n{definition.Description}";
	}
}
