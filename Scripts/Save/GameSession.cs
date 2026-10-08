using System;
using System.IO;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save.Serialization;
using Zaomeng.Save.Storage;
using Zaomeng.Level;

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
	public static LevelDefinition? SelectedLevel { get; private set; }
	public static LevelDifficultyDefinition? SelectedDifficulty { get; private set; }
	public static float SelectedSpawnSpeed { get; private set; } = 1;
	private static (string Destination, float Health, float Mana)? _entranceVitals;
	public static void SetEntranceVitals(string destination, float health, float mana)
		=> _entranceVitals = (destination, health, mana);
	public static void ClearEntranceVitals() => _entranceVitals = null;
	public static (float Health, float Mana)? TakeEntranceVitals(string destination)
	{
		var vitals = _entranceVitals;
		_entranceVitals = null;
		return vitals is { } value && value.Destination == destination ? (value.Health, value.Mana) : null;
	}
	public static void ClearLevelSelection()
	{
		SelectedLevel = null;
		SelectedDifficulty = null;
		SelectedSpawnSpeed = 1;
		ClearEntranceVitals();
	}

	public static void SelectLevel(LevelDefinition level, float spawnSpeed, LevelDifficultyDefinition difficulty)
	{
		if (string.IsNullOrWhiteSpace(level.LevelScenePath) || !ResourceLoader.Exists(level.LevelScenePath) ||
			level.MapScene is null || level.ProgressLevel < 1 || !float.IsFinite(spawnSpeed) || spawnSpeed <= 0
			|| !difficulty.IsValid || !level.Difficulties.Contains(difficulty))
			throw new ArgumentException("关卡缺少场景、地图或有效的出怪配置。");
		SelectedLevel = level;
		SelectedDifficulty = difficulty;
		SelectedSpawnSpeed = spawnSpeed;
	}

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
			SelectedLevel = null;
		SelectedDifficulty = null;
		}
		return deleted;
	}

	public static void SelectNewSlot(int slot)
	{
		ClearEntranceVitals();
		Slot = slot;
		Data = null;
		Inventory = null;
		SelectedLevel = null;
		SelectedDifficulty = null;
	}

	public static void Load(int slot)
	{
		ClearEntranceVitals();
		GameSaveData data = Manager.Load(slot);
		Slot = slot;
		Data = data;
		Inventory = null;
		SelectedLevel = null;
		SelectedDifficulty = null;
	}

	public static void BeginNewGame(ItemCatalog catalog, string characterId = "role_1")
	{
		if (Slot is < 1 or > 99) throw new InvalidOperationException("请先选择存档槽位。");
		var role = Zaomeng.Skills.SkillCatalogRegistry.Default.Get(characterId);
		Inventory = new InventoryService(25, catalog);
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
		Inventory = new InventoryService(Data?.Inventory.Capacity ?? 25, catalog);
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
		if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber));
		if (Data is null) throw new InvalidOperationException("没有正在使用的存档。");
		Data.UnlockedLevel = Math.Max(Data.UnlockedLevel, levelNumber + 1);
		Save(inventory);
	}
}
