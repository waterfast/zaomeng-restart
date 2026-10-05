using System;
using System.Linq;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Skills;
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
		foreach (SaveCharacter entry in data.Characters)
			foreach (var (slot, instance) in entry.Equipment.Slots)
				catalog.ValidateEquipmentInstance(instance, slot);
		SaveCharacter? character = data.Characters.FirstOrDefault(entry => entry.Id == data.CurrentCharacterId);
		bool updated = false;
		if (character is null)
		{
			throw new InvalidOperationException("存档的当前角色档案缺失。");
		}
		SkillCatalogRegistry.Default.Get(character.Id).ValidateCharacter(character);
		// 基础属性由角色和等级推导，不把装备或宝石加成写回基础值。
		updated |= CharacterProgression.SyncBaseStats(character);
		if (updated)
			GameSession.Save(inventory);
		return (character, inventory);
	}

}
