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
	// 空格贴图原色偏灰；统一暖色调，填充和空格共用同一底图与颜色。
	private static readonly Color SlotBackgroundTint = new(1.22f, 1.12f, 0.78f);
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
	private readonly PanelContainer _itemTooltip;
	private readonly Label _itemTooltipText;
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
		_stats = new CharacterStatsPresenter(root, character, player, catalog);
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
			button.MouseEntered += () => ShowItemTooltip(index);
			button.MouseExited += HideItemTooltip;
			button.Icon = _emptyIcon;
			button.SelfModulate = SlotBackgroundTint;
			button.TooltipText = "";
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
		(_itemTooltip, _itemTooltipText) = CreateItemTooltip();

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
		ShowItemTooltip(index);
	}

	private void RefreshItems()
	{
		HideItemTooltip();
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
		}
	}

	private (PanelContainer Panel, Label Text) CreateItemTooltip()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color("38200f"),
			BorderColor = new Color("dbaa59"),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 10,
			ContentMarginTop = 8,
			ContentMarginRight = 10,
			ContentMarginBottom = 8
		};
		var panel = new PanelContainer
		{
			Name = "ItemTooltip",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(220, 0),
			ZIndex = 100,
			Visible = false
		};
		panel.AddThemeStyleboxOverride("panel", style);
		var label = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(200, 0)
		};
		label.AddThemeColorOverride("font_color", new Color("ffe2a2"));
		label.AddThemeFontOverride("font", GD.Load<FontFile>("res://Assets/Font/8_FZCuYuan-M03S.ttf"));
		panel.AddChild(label);
		_root.AddChild(panel);
		return (panel, label);
	}

	private void ShowItemTooltip(int index)
	{
		int entryIndex = _page * SlotsPerPage + index;
		if (entryIndex >= _entries.Count)
		{
			HideItemTooltip();
			return;
		}
		_itemTooltipText.Text = ItemTooltip(_entries[entryIndex]);
		_itemTooltip.Show();
		Vector2 viewport = _root.GetViewportRect().Size;
		Vector2 cursor = _root.GetViewport().GetMousePosition();
		// 放在背包右侧，避免半透明浮层让其它格子看起来被染色。
		float x = Math.Min(viewport.X - 228, _scroll.GlobalPosition.X + _scroll.Size.X + 8);
		float y = Mathf.Clamp(cursor.Y, 8, viewport.Y - 120);
		_itemTooltip.GlobalPosition = new Vector2(x, y);
	}

	private void HideItemTooltip() => _itemTooltip.Hide();

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
			? $"攻击 +{definition.Attack}　暴击值 +{definition.CriticalRating}　命中值 +{definition.Accuracy}"
			: $"数量 {entry.Count}";
		return $"{definition.DisplayName}\n{stats}\n{definition.Description}";
	}
}
