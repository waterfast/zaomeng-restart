using System;
using System.Collections.Generic;
using System.IO;
using Zaomeng.Inventory;

namespace Zaomeng.Save;

/// <summary>开发工具修改背包存档时复用正式背包规则，避免写出超堆叠或未知物品。</summary>
public static class SaveDataEditor
{
	public static void ValidateInventory(InventorySaveData data, IItemCatalog catalog)
	{
		CreateInventory(data, catalog);
	}

	public static bool AddItem(InventorySaveData data, IItemCatalog catalog, string itemId, int amount)
	{
		InventoryService inventory = CreateInventory(data, catalog);
		if (!inventory.AddItem(itemId, amount)) return false;
		CopySlots(data, inventory.Slots);
		return true;
	}

	public static bool RemoveItem(InventorySaveData data, IItemCatalog catalog, string itemId, int amount)
	{
		InventoryService inventory = CreateInventory(data, catalog);
		if (!inventory.RemoveItem(itemId, amount)) return false;
		CopySlots(data, inventory.Slots);
		return true;
	}

	public static void ClearSlot(InventorySaveData data, IItemCatalog catalog, int index)
	{
		InventoryService inventory = CreateInventory(data, catalog);
		if ((uint)index >= (uint)inventory.Capacity)
			throw new ArgumentOutOfRangeException(nameof(index));
		var slots = new List<ItemStack?>(inventory.Slots);
		slots[index] = null;
		inventory.RestoreSlots(slots);
		CopySlots(data, inventory.Slots);
	}

	private static InventoryService CreateInventory(InventorySaveData data, IItemCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(catalog);
		if (data.Capacity <= 0 || data.Slots is null || data.Slots.Count != data.Capacity)
			throw new InvalidDataException("存档背包容量与槽位数不一致。");
		var slots = new List<ItemStack?>(data.Capacity);
		foreach (ItemStackSaveData? stack in data.Slots)
			slots.Add(stack is null ? null : new ItemStack(stack.ItemId, stack.Count));
		var inventory = new InventoryService(data.Capacity, catalog);
		inventory.RestoreSlots(slots);
		return inventory;
	}

	private static void CopySlots(InventorySaveData data, IReadOnlyList<ItemStack?> slots)
	{
		// 只在操作完整成功后回写，失败时原存档数据保持不变。
		var saved = new List<ItemStackSaveData?>(slots.Count);
		foreach (ItemStack? stack in slots)
			saved.Add(stack is null ? null : new ItemStackSaveData { ItemId = stack.ItemId, Count = stack.Count });
		data.Slots = saved;
	}
}
