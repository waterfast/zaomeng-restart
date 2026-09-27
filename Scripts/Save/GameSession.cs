using System;
using System.IO;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save.Serialization;
using Zaomeng.Save.Storage;

namespace Zaomeng.Save;

/// <summary>只保留当前槽位和跨关卡状态，文件读写交给现有存档系统。</summary>
public static class GameSession
{
	public const string FirstLevel = "res://Scenes/Level/Level_1.tscn";
	private static readonly string SaveDirectory = ProjectSettings.GlobalizePath("user://saves");
	private static readonly SaveManager Manager = new(new JsonSaveSerializer(), new PlainFileSaveStorage(SaveDirectory));
	public static int Slot { get; private set; }
	public static GameSaveData? Data { get; private set; }
	public static InventoryService? Inventory { get; private set; }

	public static bool SlotExists(int slot)
	{
		string path = Path.Combine(SaveDirectory, $"save_{slot:D2}.json");
		return File.Exists(path) || File.Exists(path + ".bak");
	}

	public static void SelectNewSlot(int slot)
	{
		Slot = slot;
		Data = null;
		Inventory = null;
	}

	public static void Load(int slot)
	{
		GameSaveData data = Manager.Load(slot);
		Slot = slot;
		Data = data;
		Inventory = null;
	}

	public static void BeginNewGame(ItemCatalog catalog)
	{
		if (Slot is < 1 or > 99) throw new InvalidOperationException("请先选择存档槽位。");
		Inventory = new InventoryService(70, catalog);
		Data = InventorySaveMapper.Capture(Inventory);
		Data.Characters.Add(new Zaomeng.Character.Character { Id = "role_1", Name = "孙悟空" });
		Manager.Save(Slot, Data);
	}

	public static InventoryService PrepareInventory(ItemCatalog catalog)
	{
		if (Inventory != null) return Inventory;
		Inventory = new InventoryService(Data?.Inventory.Capacity ?? 70, catalog);
		if (Data is not null) InventorySaveMapper.Restore(Data, Inventory);
		return Inventory;
	}

	public static void Save(InventoryService inventory)
	{
		if (Slot == 0) return;
		Data = InventorySaveMapper.Capture(inventory, Data);
		Manager.Save(Slot, Data);
	}
}
