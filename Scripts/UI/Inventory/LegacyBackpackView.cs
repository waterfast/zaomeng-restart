using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Events;
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
	private readonly GameplayEvents _events;
	private readonly IDisposable _equipmentConnection;
	private readonly IDisposable _modificationConnection;
	private readonly IDisposable _saveFailureConnection;
	private readonly AcceptDialog _feedback;
	private readonly GemSocketView _gemSockets;
	private readonly ItemActionMenu _actionMenu;
	private readonly IDisposable _itemActionConnection;
	private readonly List<(Control Control, Control.GuiInputEventHandler Handler)> _activationHandlers = new();
	private readonly List<(Button Button, Action Handler)> _equipmentClickHandlers = new();
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
	private readonly LegacyItemTooltip _itemTooltip;
	public CharacterStatRegistry StatRegistry => _stats.Registry;
	private readonly Dictionary<string, (Button Button, TextureRect Icon, Texture2D? EmptyIcon)> _equipmentSlots = new();
	private readonly List<(Button Button, Action Enter, Action Exit)> _equipmentHoverHandlers = new();
	private ItemCategory _category = ItemCategory.Equipment;
	private IReadOnlyList<InventoryDisplayEntry> _entries = Array.Empty<InventoryDisplayEntry>();
	private int _page;
	private int? _selectedSlot;

	public event Action? CloseRequested;

	public LegacyBackpackView(Node2D root, InventoryViewAdapter adapter, ItemCatalog catalog,
		SaveCharacter character, Wallet wallet, Player player, Action saveChanges,
		GameplayEvents events,
		CharacterStatRegistry? statRegistry = null)
	{
		_root = root;
		_adapter = adapter;
		_catalog = catalog;
		_character = character;
		_wallet = wallet;
		_saveChanges = saveChanges;
		_events = events;
		events.Validate();
		_feedback = new AcceptDialog { Name = "EquipmentFeedback", Title = "物品提示" };
		_root.AddChild(_feedback);
		_gemSockets = new GemSocketView(root, adapter, character, catalog, events, ShowFeedback);
		_actionMenu = new ItemActionMenu(root, events, ShowFeedback);
		_itemActionConnection = events.ItemActionCompleted.Subscribe(Refresh);
		_equipmentConnection = events.EquipmentChanged.Subscribe(OnEquipmentChanged);
		_modificationConnection = events.EquipmentModified.Subscribe(change =>
		{
			if (change.CharacterId == _character.Id) Refresh();
		});
		_saveFailureConnection = events.SaveFailed.Subscribe(ShowFeedback);
		_stats = new CharacterStatsPresenter(root, character, player, catalog, statRegistry);
		_emptyIcon = GD.Load<Texture2D>("res://Assets/Art/BackPack/AllItems/empty.png");
		_tabNormal = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button.png");
		_tabSelected = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button_choose.png");
		_scroll = root.GetNode<ScrollContainer>(
			"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box");
		_scroll.GetVScrollBar().ValueChanged += OnScrollChanged;
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
			Control.GuiInputEventHandler activate = input => OnSlotInput(index, input);
			button.GuiInput += activate;
			_activationHandlers.Add((button, activate));
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
		_itemTooltip = new LegacyItemTooltip(root);

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
		_gemSockets.Dispose();
		_actionMenu.Dispose();
		_itemActionConnection.Dispose();
		_equipmentConnection.Dispose();
		_modificationConnection.Dispose();
		_saveFailureConnection.Dispose();
		foreach (var (control, handler) in _activationHandlers) control.GuiInput -= handler;
		foreach (var (button, handler) in _equipmentClickHandlers) button.Pressed -= handler;
		foreach (var (button, enter, exit) in _equipmentHoverHandlers)
		{
			button.MouseEntered -= enter;
			button.MouseExited -= exit;
		}
		_feedback.QueueFree();
		_stats.Dispose();
		_adapter.Changed -= RefreshItems;
		_root.VisibilityChanged -= OnVisibilityChanged;
		_scroll.GetVScrollBar().ValueChanged -= OnScrollChanged;
	}

	private void OnScrollChanged(double value)
	{
		_actionMenu.Hide();
		_itemTooltip.Hide();
	}

	private void OnVisibilityChanged()
	{
		if (_root.Visible) Refresh();
		else { _itemTooltip.Hide(); _actionMenu.Hide(); _gemSockets.Hide(); _feedback.Hide(); }
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
		InventoryDisplayEntry entry = _entries[entryIndex];
		Button slot = _slots[index];
		Rect2 area = slot.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, slot.Size);
		_actionMenu.Show(new(entry.SlotIndex, entry.Definition.Id, entry.Equipment?.InstanceId ?? ""), area, _page, index);
	}

	private void OnSlotInput(int index, InputEvent input)
	{
		if (input is not InputEventMouseButton { Pressed: true } mouse) return;
		int entryIndex = _page * SlotsPerPage + index;
		if (entryIndex >= _entries.Count) return;
		InventoryDisplayEntry entry = _entries[entryIndex];
		if (entry.Definition.Category != ItemCategory.Equipment) return;
		if (mouse.ButtonIndex == MouseButton.Right && entry.Equipment is not null)
		{
			_itemTooltip.Hide();
			_actionMenu.Hide();
			_gemSockets.Show(entry.Equipment);
			return;
		}
	}

	private void OnEquipmentChanged(EquipmentChange change)
	{
		if (change.CharacterId != _character.Id) return;
		_selectedSlot = null;
		Refresh();
	}

	private void ShowFeedback(string message)
	{
		_feedback.DialogText = message;
		_feedback.PopupCentered();
	}

	private void RefreshItems()
	{
		HideItemTooltip();
		_actionMenu.Hide();
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

	private void ShowItemTooltip(int index)
	{
		if (_actionMenu.Visible) return;
		int entryIndex = _page * SlotsPerPage + index;
		if (entryIndex >= _entries.Count)
		{
			HideItemTooltip();
			return;
		}
		InventoryDisplayEntry entry = _entries[entryIndex];
		Button slot = _slots[index];
		Rect2 slotArea = slot.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, slot.Size);
		_itemTooltip.Show(entry.Definition, entry.Count, slotArea, entry.Equipment, _catalog);
	}

	private void HideItemTooltip() => _itemTooltip.Hide();

	private void BindEquipmentSlots()
	{
		const string information = "background/infomation";
		AddEquipmentSlot("wq", $"{information}/equ_/HBoxContainer/wq", EquipmentSlot.Weapon);
		AddEquipmentSlot("fj", $"{information}/equ_/HBoxContainer/fj", EquipmentSlot.Armor);
		AddEquipmentSlot("sp", $"{information}/equ_/VBoxContainer/sp", EquipmentSlot.Accessory);
		AddEquipmentSlot("fb", $"{information}/equ_/VBoxContainer/fb", EquipmentSlot.MagicWeapon);
		AddEquipmentSlot("tx", $"{information}/tx", EquipmentSlot.Title);
		AddEquipmentSlot("sz", $"{information}/sz", EquipmentSlot.Costume);
		AddEquipmentSlot("cb", $"{information}/cb", EquipmentSlot.Wing);
	}

	private void AddEquipmentSlot(string key, string path, EquipmentSlot slot)
	{
		Button button = _root.GetNode<Button>(path);
		var icon = new TextureRect
		{
			Name = "ItemIcon", MouseFilter = Control.MouseFilterEnum.Ignore,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Material = _itemIcons[0].Material
		};
		_equipmentSlots.Add(key, (button, icon, button.Icon));
		button.AddChild(icon);
		icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		button.Icon = _emptyIcon;
		button.SelfModulate = SlotBackgroundTint;
		button.TooltipText = "";
		Action openMenu = () =>
		{
			_itemTooltip.Hide();
			if (_character.Equipment.Get(slot) is not EquipmentInstance instance) { _actionMenu.Hide(); return; }
			Rect2 area = button.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, button.Size);
			_actionMenu.Show(new(-1, instance.DefinitionId, instance.InstanceId, slot), area, caption: "已穿戴");
		};
		button.Pressed += openMenu;
		_equipmentClickHandlers.Add((button, openMenu));
		Action hover = () =>
		{
			if (_actionMenu.Visible || _character.Equipment.Get(slot) is not EquipmentInstance instance ||
				!_catalog.TryGetDefinition(instance.DefinitionId, out ItemDefinition? definition)) return;
			Rect2 area = button.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, button.Size);
			_itemTooltip.Show(definition!, 1, area, instance, _catalog);
		};
		button.MouseEntered += hover;
		button.MouseExited += HideItemTooltip;
		_equipmentHoverHandlers.Add((button, hover, HideItemTooltip));
		Control.GuiInputEventHandler socketInput = input =>
		{
			if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } &&
				_character.Equipment.Get(slot) is EquipmentInstance instance)
			{
				_itemTooltip.Hide(); _actionMenu.Hide(); _gemSockets.Show(instance);
			}
		};
		button.GuiInput += socketInput;
		_activationHandlers.Add((button, socketInput));
	}

	private void RefreshEquipment()
	{
		EquipmentLoadout equipment = _character.Equipment;
		ShowEquipment("wq", equipment.Get(EquipmentSlot.Weapon));
		ShowEquipment("fj", equipment.Get(EquipmentSlot.Armor));
		ShowEquipment("sp", equipment.Get(EquipmentSlot.Accessory));
		ShowEquipment("fb", equipment.Get(EquipmentSlot.MagicWeapon));
		ShowEquipment("tx", equipment.Get(EquipmentSlot.Title));
		ShowEquipment("sz", equipment.Get(EquipmentSlot.Costume));
		ShowEquipment("cb", equipment.Get(EquipmentSlot.Wing));
	}

	private void ShowEquipment(string key, EquipmentInstance? instance)
	{
		(Button button, TextureRect icon, Texture2D? emptyIcon) = _equipmentSlots[key];
		string itemId = instance?.DefinitionId ?? "";
		if (_catalog.TryGetDefinition(itemId, out ItemDefinition? definition))
		{
			icon.Texture = definition!.Icon ?? emptyIcon;
		}
		else
		{
			icon.Texture = emptyIcon;
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

}
