using System;
using System.Collections.Generic;
using Zaomeng.Inventory;

namespace Zaomeng.Save;

/// <summary>只映射持久背包；场景、坐标和当前血量每次进关重新创建。</summary>
public static class InventorySaveMapper
{
	public static GameSaveData Capture(InventoryService inventory, GameSaveData? existingSave = null)
	{
		ArgumentNullException.ThrowIfNull(inventory);
		var data = existingSave ?? new GameSaveData();
		var savedInventory = new InventorySaveData { Capacity = inventory.Capacity };
		foreach (ItemStack? stack in inventory.Slots)
			savedInventory.Slots.Add(stack is null ? null : new ItemStackSaveData
			{
				ItemId = stack.ItemId,
				Count = stack.Count
			});
		data.Inventory = savedInventory;
		SaveDataValidator.Validate(data);
		return data;
	}

	public static void Restore(GameSaveData data, InventoryService inventory)
	{
		ArgumentNullException.ThrowIfNull(inventory);
		SaveDataValidator.Validate(data);
		var slots = new List<ItemStack?>(data.Inventory.Capacity);
		foreach (ItemStackSaveData? stack in data.Inventory.Slots)
			slots.Add(stack is null ? null : new ItemStack(stack.ItemId, stack.Count));
		inventory.RestoreSlots(slots);
	}
}
