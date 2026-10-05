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
	public const string FirstMap = "res://Scenes/UI/MainMenu/Map1.tscn";
	private static readonly string SaveDirectory = ProjectSettings.GlobalizePath("user://saves");
	private static readonly SaveManager Manager = new(new JsonSaveSerializer(), new PlainFileSaveStorage(SaveDirectory));
	public static int Slot { get; private set; }
	public static GameSaveData? Data { get; private set; }
	public static InventoryService? Inventory { get; private set; }

	public static bool SlotExists(int slot)
		=> SaveSlotFiles.Exists(SaveDirectory, slot, "json");

	public static string GetSlotSummary(int slot)
	{
		try
		{
			GameSaveData data = Manager.Load(slot);
			Zaomeng.Character.Character? character = data.Characters.Find(entry => entry.Id == data.CurrentCharacterId);
			if (character is null) return "无角色档案";
			string name = string.IsNullOrWhiteSpace(character.Name) ? character.Id : character.Name;
			return $"{name} Lv.{character.Level}";
		}
		catch (Exception) { return "存档无法读取"; }
	}

	public static bool DeleteSlot(int slot)
	{
		bool deleted = SaveSlotFiles.Delete(SaveDirectory, slot);
		if (deleted && Slot == slot)
		{
			Slot = 0;
			Data = null;
			Inventory = null;
		}
		return deleted;
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

	public static void BeginNewGame(ItemCatalog catalog, string characterId = "role_1")
	{
		if (Slot is < 1 or > 99) throw new InvalidOperationException("请先选择存档槽位。");
		var role = Zaomeng.Skills.SkillCatalogRegistry.Default.Get(characterId);
		Inventory = new InventoryService(70, catalog);
		Data = InventorySaveMapper.Capture(Inventory);
		Data.CurrentCharacterId = characterId;
		var character = new Zaomeng.Character.Character { Id = characterId, Name = role.DisplayName, Level = 1 };
		Zaomeng.Character.CharacterProgression.SyncBaseStats(character);
		Data.Characters.Add(character);
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

	public static void CompleteLevel(int levelNumber, InventoryService inventory)
	{
		if (levelNumber is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(levelNumber));
		if (Data is null) throw new InvalidOperationException("没有正在使用的存档。");
		Data.UnlockedLevel = Math.Max(Data.UnlockedLevel, levelNumber + 1);
		Save(inventory);
	}
}
