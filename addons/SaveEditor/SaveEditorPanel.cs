#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.Save.Storage;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.EditorTools;

[Tool]
public partial class SaveEditorPanel : VBoxContainer
{
	private readonly JsonSaveSerializer _serializer = new();
	private readonly List<ItemDefinition> _definitions = new();
	private readonly List<SaveSlotFile> _listedSaves = new();
	private ItemCatalog? _catalog;
	private GameSaveData? _data;
	private string? _loadedPath;
	private DateTime _loadedWriteTime;
	private string? _loadedKeyIdentity;
	private int _selectedCharacterIndex = -1;
	private bool _updatingJson;
	private bool _jsonDirty;

	private SpinBox _slot = null!;
	private OptionButton _existingSaves = null!;
	private SpinBox _newCapacity = null!;
	private OptionButton _format = null!;
	private LineEdit _key = null!;
	private HBoxContainer _keyRow = null!;
	private Button _saveButton = null!;
	private Label _status = null!;
	private LineEdit _souls = null!;
	private LineEdit _coupons = null!;
	private OptionButton _characters = null!;
	private LineEdit _newCharacterId = null!;
	private LineEdit _characterName = null!;
	private SpinBox _level = null!;
	private LineEdit _experience = null!;
	private ItemList _inventory = null!;
	private OptionButton _item = null!;
	private SpinBox _amount = null!;
	private TextEdit _json = null!;
	private ConfirmationDialog _deleteConfirmation = null!;
	private int _pendingDeleteSlot;

	public override void _Ready()
	{
		Name = "存档管理";
		CustomMinimumSize = new Vector2(440, 390);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		BuildUi();
		LoadCatalog();
		RefreshSaveList();
		if (_catalog is not null)
			SetStatus("选择槽位后读取存档；新建只在内存中创建，点击保存才写入文件。");
	}

	private void BuildUi()
	{
		var existingRow = AddRow(this);
		AddLabel(existingRow, "已有存档");
		_existingSaves = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_existingSaves.ItemSelected += OnExistingSaveSelected;
		existingRow.AddChild(_existingSaves);
		AddButton(existingRow, "刷新", RefreshSaveList);

		var selectionRow = AddRow(this);
		AddLabel(selectionRow, "槽位");
		_slot = AddSpin(selectionRow, 1, 99, 1);
		_slot.CustomMinimumSize = new Vector2(65, 0);
		AddLabel(selectionRow, "格式");
		_format = new OptionButton();
		_format.AddItem("开发 JSON");
		_format.AddItem("加密 DAT");
		_format.ItemSelected += _ => OnFormatSelected();
		selectionRow.AddChild(_format);
		AddLabel(selectionRow, "新建容量");
		_newCapacity = AddSpin(selectionRow, 1, 999, 70);
		_newCapacity.CustomMinimumSize = new Vector2(70, 0);
		var actionRow = AddRow(this);
		AddButton(actionRow, "读取", LoadSave);
		AddButton(actionRow, "添加存档", CreateSave);
		_saveButton = AddButton(actionRow, "保存", Save);
		_saveButton.Disabled = true;
		AddButton(actionRow, "删除存档", RequestDelete);
		_status = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		AddChild(_status);

		_keyRow = AddRow(this);
		AddLabel(_keyRow, "密钥");
		_key = new LineEdit
		{
			Secret = true,
			PlaceholderText = "留空时读取 ZAOMENG_SAVE_KEY 环境变量",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_keyRow.AddChild(_key);
		_keyRow.Visible = false;

		var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		AddChild(tabs);
		var basic = new VBoxContainer { Name = "角色与货币", SizeFlagsVertical = SizeFlags.ExpandFill };
		tabs.AddChild(basic);
		BuildPersistentDataColumn(basic);
		var inventoryTab = new VBoxContainer { Name = "背包", SizeFlagsVertical = SizeFlags.ExpandFill };
		tabs.AddChild(inventoryTab);
		BuildInventoryColumn(inventoryTab);
		var advanced = new VBoxContainer { Name = "完整 JSON" };
		tabs.AddChild(advanced);
		AddLabel(advanced, "可修改所有存档字段。编辑后先点“应用 JSON”，通过校验后再保存。");
		_json = new TextEdit { SizeFlagsVertical = SizeFlags.ExpandFill };
		_json.TextChanged += () => { if (!_updatingJson) _jsonDirty = true; };
		advanced.AddChild(_json);
		AddButton(advanced, "应用 JSON", ApplyJson);
		tabs.TabChanged += _ => SyncBasicFieldsToJson();
		_deleteConfirmation = new ConfirmationDialog { Title = "删除存档" };
		_deleteConfirmation.Confirmed += ConfirmDelete;
		AddChild(_deleteConfirmation);
	}

	private void BuildPersistentDataColumn(Control parent)
	{
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		parent.AddChild(scroll);
		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		scroll.AddChild(column);
		AddLabel(column, "角色与货币（关卡每次重开）");
		_souls = AddLine(column, "灵魂");
		_coupons = AddLine(column, "点券");
		AddLabel(column, "角色档案");
		var characterRow = AddRow(column);
		_characters = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_characters.ItemSelected += OnCharacterSelected;
		characterRow.AddChild(_characters);
		var newCharacterRow = AddRow(column);
		_newCharacterId = new LineEdit { PlaceholderText = "新角色 ID", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		newCharacterRow.AddChild(_newCharacterId);
		AddButton(newCharacterRow, "新增", AddCharacter);
		_characterName = AddLine(column, "名字");
		_level = AddLabeledSpin(column, "等级", 1, int.MaxValue, 1);
		_experience = AddLine(column, "经验");
	}

	private void BuildInventoryColumn(Control parent)
	{
		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		parent.AddChild(column);
		AddLabel(column, "背包槽位");
		_inventory = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
		column.AddChild(_inventory);
		var itemRow = AddRow(column);
		_item = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		itemRow.AddChild(_item);
		var itemActionRow = AddRow(column);
		_amount = AddSpin(itemActionRow, 1, int.MaxValue, 1);
		_amount.CustomMinimumSize = new Vector2(95, 0);
		AddButton(itemActionRow, "增加", () => ChangeItem(add: true));
		AddButton(itemActionRow, "扣除", () => ChangeItem(add: false));
		AddButton(column, "清空选中槽位", ClearSelectedSlot);
	}

	private void LoadCatalog()
	{
		_catalog = ResourceLoader.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		if (_catalog is null)
		{
			SetStatus("无法加载物品目录 Content/Items/ItemCatalog.tres。");
			return;
		}
		try { _catalog.Validate(); }
		catch (Exception error)
		{
			_catalog = null;
			SetStatus("物品目录无效：" + error.Message);
			return;
		}
		foreach (ItemDefinition definition in _catalog.Definitions)
		{
			_definitions.Add(definition);
			_item.AddItem($"{definition.DisplayName} ({definition.Id})");
		}
	}

	private void OnFormatSelected()
	{
		_keyRow.Visible = _format.Selected == 1;
		SetStatus(_format.Selected == 1
			? "当前游戏仅读取开发 JSON；加密 DAT 用于验证格式。切换格式后请重新读取存档。"
			: "当前游戏读取开发 JSON。切换格式后请重新读取存档。");
	}

	private void RefreshSaveList() => TryAction(() =>
	{
		string directory = GetSaveDirectory();
		_listedSaves.Clear();
		_existingSaves.Clear();
		_existingSaves.AddItem("选择默认目录中的存档");
		var jsonManager = new SaveManager(_serializer, new PlainFileSaveStorage(directory));
		int selectedIndex = 0;
		foreach (SaveSlotFile file in SaveSlotFiles.List(directory))
		{
			_listedSaves.Add(file);
			string description = file.Extension == "json"
				? DescribeJsonSave(jsonManager, file.Slot)
				: "加密存档";
			string suffix = file.BackupOnly ? "（仅备份）" : "";
			_existingSaves.AddItem($"{file.Slot:D2} · {file.Extension.ToUpperInvariant()} · {description}{suffix}");
			if (file.Slot == (int)_slot.Value && file.Extension == SelectedExtension())
				selectedIndex = _listedSaves.Count;
		}
		_existingSaves.Select(selectedIndex);
	});

	private static string DescribeJsonSave(SaveManager manager, int slot)
	{
		try
		{
			GameSaveData data = manager.Load(slot);
			SaveCharacter? character = data.Characters.Find(entry => entry.Id == "role_1")
				?? (data.Characters.Count > 0 ? data.Characters[0] : null);
			if (character is null) return "无角色档案";
			string name = string.IsNullOrWhiteSpace(character.Name) ? character.Id : character.Name;
			return $"{name} Lv.{character.Level}";
		}
		catch (Exception) { return "无法读取"; }
	}

	private void OnExistingSaveSelected(long index)
	{
		if (index < 1 || index > _listedSaves.Count) return;
		SaveSlotFile file = _listedSaves[(int)index - 1];
		_slot.Value = file.Slot;
		_format.Select(file.Extension == "dat" ? 1 : 0);
		_keyRow.Visible = file.Extension == "dat";
		SetStatus($"已选择存档 {file.Slot:D2}，点击读取后即可修改。");
	}

	private void RequestDelete() => TryAction(() =>
	{
		int slot = (int)_slot.Value;
		if (!SaveSlotFiles.Exists(GetSaveDirectory(), slot, "json") &&
			!SaveSlotFiles.Exists(GetSaveDirectory(), slot, "dat"))
			throw new InvalidOperationException($"存档 {slot:D2} 不存在。");
		_pendingDeleteSlot = slot;
		_deleteConfirmation.DialogText = $"确定删除存档 {slot:D2}？\nJSON、DAT、备份和临时文件都会删除，无法恢复。";
		_deleteConfirmation.PopupCentered();
	});

	private void ConfirmDelete() => TryAction(() =>
	{
		int slot = _pendingDeleteSlot;
		if (!SaveSlotFiles.Delete(GetSaveDirectory(), slot))
			throw new InvalidOperationException("存档文件不存在。");
		ClearCurrentData();
		RefreshSaveList();
		SetStatus($"已删除存档 {slot:D2}。可在同一槽位添加新存档。");
	});

	private void ClearCurrentData()
	{
		_data = null;
		_loadedPath = null;
		_loadedKeyIdentity = null;
		_loadedWriteTime = DateTime.MinValue;
		_selectedCharacterIndex = -1;
		_saveButton.Disabled = true;
		_characters.Clear();
		ShowCharacterFields();
		_souls.Text = "";
		_coupons.Text = "";
		_inventory.Clear();
		_updatingJson = true;
		_json.Text = "";
		_updatingJson = false;
		_jsonDirty = false;
	}

	private void LoadSave() => TryAction(() =>
	{
		SaveManager manager = CreateManager();
		GameSaveData loaded = manager.Load((int)_slot.Value);
		if (_catalog is null) throw new InvalidOperationException("物品目录未加载，不能验证存档背包。");
		SaveDataEditor.ValidateInventory(loaded.Inventory, _catalog);
		_data = loaded;
		RememberFile();
		RefreshAll();
		SetStatus("已读取存档。修改后点击保存。");
	});

	private void CreateSave() => TryAction(() =>
	{
		string directory = GetSaveDirectory();
		int slot = (int)_slot.Value;
		if (SaveSlotFiles.Exists(directory, slot, "json") || SaveSlotFiles.Exists(directory, slot, "dat"))
			throw new InvalidOperationException("该槽位已有存档，请先读取或切换空槽。");
		CurrentKeyIdentity();
		_data = new GameSaveData
		{
			Inventory = new InventorySaveData { Capacity = (int)_newCapacity.Value }
		};
		_data.Characters.Add(new SaveCharacter { Id = "role_1", Name = "孙悟空" });
		for (int i = 0; i < _data.Inventory.Capacity; i++) _data.Inventory.Slots.Add(null);
		RememberFile();
		RefreshAll();
		SetStatus("新存档已创建在内存中，点击保存写入文件。");
	});

	private void Save() => TryAction(() =>
	{
		EnsureLoaded();
		EnsureJsonApplied();
		if (_loadedPath != CurrentPath() || _loadedKeyIdentity != CurrentKeyIdentity())
			throw new InvalidOperationException("槽位、格式或密钥已改变，请重新读取存档。");
		DateTime currentWriteTime = File.Exists(_loadedPath) ? File.GetLastWriteTimeUtc(_loadedPath) : DateTime.MinValue;
		if (currentWriteTime != _loadedWriteTime)
			throw new InvalidOperationException("文件已被游戏或其他程序修改，请重新读取后再编辑。");
		CommitBasicFields();
		if (_catalog is null) throw new InvalidOperationException("物品目录未加载，不能验证存档背包。");
		SaveDataEditor.ValidateInventory(_data!.Inventory, _catalog);
		CreateManager().Save((int)_slot.Value, _data!);
		RememberFile();
		RefreshJson();
		RefreshSaveList();
		SetStatus("已保存；如有先前的有效存档，旧文件保存在同目录的 .bak 文件中。");
	});

	private void ChangeItem(bool add) => TryAction(() =>
	{
		EnsureLoaded();
		EnsureJsonApplied();
		if (_catalog is null || _item.Selected < 0) throw new InvalidOperationException("没有可用的物品目录。");
		CommitBasicFields();
		ItemDefinition definition = _definitions[_item.Selected];
		int amount = checked((int)_amount.Value);
		bool success = add
			? SaveDataEditor.AddItem(_data!.Inventory, _catalog, definition.Id, amount)
			: SaveDataEditor.RemoveItem(_data!.Inventory, _catalog, definition.Id, amount);
		if (!success) throw new InvalidOperationException(add ? "背包空间不足，或物品不在目录中。" : "道具数量不足。");
		RefreshInventory();
		RefreshJson();
		SetStatus($"已在内存中{(add ? "增加" : "扣除")} {definition.DisplayName} × {amount}；点击保存写入文件。");
	});

	private void ClearSelectedSlot() => TryAction(() =>
	{
		EnsureLoaded();
		EnsureJsonApplied();
		if (_catalog is null) throw new InvalidOperationException("没有可用的物品目录。");
		var selected = _inventory.GetSelectedItems();
		if (selected.Length == 0) throw new InvalidOperationException("请先选择一个背包槽位。");
		SaveDataEditor.ClearSlot(_data!.Inventory, _catalog, selected[0]);
		RefreshInventory();
		RefreshJson();
		SetStatus("已清空槽位；点击保存写入文件。");
	});

	private void AddCharacter() => TryAction(() =>
	{
		EnsureLoaded();
		EnsureJsonApplied();
		CommitBasicFields();
		string id = _newCharacterId.Text.Trim();
		if (id.Length == 0) throw new InvalidOperationException("请输入角色 ID。");
		if (_data!.Characters.Exists(character => character.Id == id))
			throw new InvalidOperationException("角色 ID 已存在。");
		_data.Characters.Add(new SaveCharacter { Id = id, Name = id });
		_newCharacterId.Text = "";
		RefreshCharacters(_data.Characters.Count - 1);
		RefreshJson();
		SetStatus("已添加角色档案；点击保存写入文件。");
	});

	private void OnCharacterSelected(long index)
	{
		if (_data is null) return;
		try
		{
			CommitCharacterFields();
			_selectedCharacterIndex = (int)index;
			ShowCharacterFields();
		}
		catch (Exception error)
		{
			if (_selectedCharacterIndex >= 0) _characters.Select(_selectedCharacterIndex);
			SetStatus(error.Message);
		}
	}

	private void ApplyJson() => TryAction(() =>
	{
		EnsureLoaded();
		GameSaveData parsed = _serializer.Deserialize(Encoding.UTF8.GetBytes(_json.Text));
		if (_catalog is null) throw new InvalidOperationException("物品目录未加载，不能验证存档背包。");
		SaveDataEditor.ValidateInventory(parsed.Inventory, _catalog);
		_data = parsed;
		RefreshAll();
		SetStatus("JSON 已应用到内存；点击保存写入文件。");
	});

	private void CommitBasicFields()
	{
		EnsureLoaded();
		_data!.Wallet.Souls = ParseNonnegativeLong(_souls, "灵魂");
		_data.Wallet.Coupons = ParseNonnegativeLong(_coupons, "点券");
		CommitCharacterFields();
	}

	private void SyncBasicFieldsToJson()
	{
		if (_data is null || _jsonDirty) return;
		TryAction(() =>
		{
			CommitBasicFields();
			RefreshJson();
		});
	}

	private void CommitCharacterFields()
	{
		if (_data is null || _selectedCharacterIndex < 0 || _selectedCharacterIndex >= _data.Characters.Count) return;
		SaveCharacter character = _data.Characters[_selectedCharacterIndex];
		character.Name = _characterName.Text;
		character.Level = (int)_level.Value;
		character.Experience = ParseNonnegativeLong(_experience, "经验");
	}

	private void RefreshAll()
	{
		_saveButton.Disabled = _data is null;
		if (_data is null) return;
		_souls.Text = _data.Wallet.Souls.ToString();
		_coupons.Text = _data.Wallet.Coupons.ToString();
		RefreshCharacters(0);
		RefreshInventory();
		RefreshJson();
	}

	private void RefreshCharacters(int selectedIndex)
	{
		_characters.Clear();
		if (_data is null) return;
		foreach (SaveCharacter character in _data.Characters)
			_characters.AddItem($"{character.Name} ({character.Id})");
		_selectedCharacterIndex = _data.Characters.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, _data.Characters.Count - 1);
		if (_selectedCharacterIndex >= 0) _characters.Select(_selectedCharacterIndex);
		ShowCharacterFields();
	}

	private void ShowCharacterFields()
	{
		bool hasCharacter = _data is not null && _selectedCharacterIndex >= 0;
		_characterName.Editable = hasCharacter;
		_level.Editable = hasCharacter;
		_experience.Editable = hasCharacter;
		_characterName.Text = hasCharacter ? _data!.Characters[_selectedCharacterIndex].Name : "";
		_level.Value = hasCharacter ? _data!.Characters[_selectedCharacterIndex].Level : 1;
		_experience.Text = hasCharacter ? _data!.Characters[_selectedCharacterIndex].Experience.ToString() : "";
	}

	private void RefreshInventory()
	{
		_inventory.Clear();
		if (_data is null) return;
		for (int i = 0; i < _data.Inventory.Slots.Count; i++)
		{
			ItemStackSaveData? stack = _data.Inventory.Slots[i];
			string text = stack is null ? "空" : $"{GetItemName(stack.ItemId)} × {stack.Count}";
			_inventory.AddItem($"{i + 1:D2}  {text}");
		}
	}

	private string GetItemName(string id) =>
		_catalog is not null && _catalog.TryGetDefinition(id, out ItemDefinition? definition)
			? definition!.DisplayName : id;

	private void RefreshJson()
	{
		if (_data is null) return;
		_updatingJson = true;
		try { _json.Text = Encoding.UTF8.GetString(_serializer.Serialize(_data)); }
		finally { _updatingJson = false; _jsonDirty = false; }
	}

	private SaveManager CreateManager()
	{
		string directory = GetSaveDirectory();
		ISaveStorage storage = _format.Selected == 0
			? new PlainFileSaveStorage(directory)
			: new EncryptedFileSaveStorage(directory, ReadKey());
		return new SaveManager(_serializer, storage);
	}

	private byte[] ReadKey()
	{
		string text = _key.Text.Trim();
		if (text.Length == 0) text = System.Environment.GetEnvironmentVariable("ZAOMENG_SAVE_KEY") ?? "";
		if (text.Length != 64) throw new InvalidOperationException("加密密钥须为 64 位十六进制文本。");
		try { return Convert.FromHexString(text); }
		catch (FormatException) { throw new InvalidOperationException("加密密钥包含非十六进制字符。"); }
	}

	private string CurrentKeyIdentity() =>
		_format.Selected == 0 ? "plain" : Convert.ToHexString(SHA256.HashData(ReadKey()));

	private string CurrentPath()
		=> SaveSlotFiles.GetPath(GetSaveDirectory(), (int)_slot.Value, SelectedExtension());

	private static string GetSaveDirectory() => ProjectSettings.GlobalizePath("user://saves");

	private string SelectedExtension() => _format.Selected == 0 ? "json" : "dat";

	private void RememberFile()
	{
		_loadedPath = CurrentPath();
		_loadedWriteTime = File.Exists(_loadedPath) ? File.GetLastWriteTimeUtc(_loadedPath) : DateTime.MinValue;
		_loadedKeyIdentity = CurrentKeyIdentity();
	}

	private void EnsureLoaded()
	{
		if (_data is null) throw new InvalidOperationException("请先读取或新建存档。");
	}

	private void EnsureJsonApplied()
	{
		if (_jsonDirty) throw new InvalidOperationException("完整 JSON 有未应用的修改，请先点击“应用 JSON”。");
	}

	private static long ParseNonnegativeLong(LineEdit field, string label)
	{
		if (!long.TryParse(field.Text, out long value) || value < 0)
			throw new FormatException($"{label}必须是非负整数。");
		return value;
	}

	private void TryAction(Action action)
	{
		try { action(); }
		catch (Exception error) { SetStatus("操作失败：" + error.Message); }
	}

	private void SetStatus(string message) => _status.Text = message;

	private static HBoxContainer AddRow(Node parent)
	{
		var row = new HBoxContainer();
		parent.AddChild(row);
		return row;
	}

	private static Label AddLabel(Node parent, string text)
	{
		var label = new Label { Text = text };
		parent.AddChild(label);
		return label;
	}

	private static Button AddButton(Node parent, string text, Action action)
	{
		var button = new Button { Text = text };
		button.Pressed += action;
		parent.AddChild(button);
		return button;
	}

	private static SpinBox AddSpin(Node parent, double min, double max, double value)
	{
		var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = value };
		parent.AddChild(spin);
		return spin;
	}

	private static SpinBox AddLabeledSpin(Node parent, string label, double min, double max, double value)
	{
		var row = AddRow(parent);
		AddLabel(row, label);
		SpinBox spin = AddSpin(row, min, max, value);
		spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return spin;
	}

	private static LineEdit AddLine(Node parent, string label)
	{
		var row = AddRow(parent);
		AddLabel(row, label);
		var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddChild(field);
		return field;
	}
}
#endif
