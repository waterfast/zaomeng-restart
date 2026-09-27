using System;
using System.Linq;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Save;

/// <summary>进入关卡时恢复当前角色和背包；直接运行场景时使用一号存档。</summary>
public static class GameSessionCharacter
{
	public static (SaveCharacter Character, InventoryService Inventory) Prepare(ItemCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(catalog);
		catalog.Validate();
		if (GameSession.Data is null)
		{
			int slot = GameSession.Slot == 0 ? 1 : GameSession.Slot;
			if (GameSession.SlotExists(slot))
				GameSession.Load(slot);
			else
			{
				GameSession.SelectNewSlot(slot);
				GameSession.BeginNewGame(catalog);
			}
		}

		InventoryService inventory = GameSession.PrepareInventory(catalog);
		GameSaveData data = GameSession.Data!;
		SaveCharacter? character = data.Characters.FirstOrDefault(entry => entry.Id == "role_1")
			?? data.Characters.FirstOrDefault();
		bool updated = false;
		if (character is null)
		{
			character = new SaveCharacter { Id = "role_1", Name = "孙悟空" };
			data.Characters.Add(character);
			updated = true;
		}
		// 兼容早期只有背包的存档；仅在基础属性全空时补入演示值。
		if (character.BaseStats.MaxHealth == 0 && character.BaseStats.MaxMana == 0 &&
			character.BaseStats.Attack == 0)
		{
			character.BaseStats = CreateStartingStats();
			updated = true;
		}
		if (updated)
			GameSession.Save(inventory);
		return (character, inventory);
	}

	private static CharacterStats CreateStartingStats() => new()
	{
		MaxHealth = 120,
		MaxMana = 80,
		Attack = 12,
		PhysicalDefense = 15,
		MagicDefense = 10,
		CriticalRating = 10,
		DodgeRating = 5,
		HealthRegeneration = 1,
		ManaRegeneration = 0.5f,
		Luck = 5,
		Toughness = 3,
		Accuracy = 4
	};
}
