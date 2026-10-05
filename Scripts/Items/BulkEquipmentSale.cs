using System;
using System.Linq;
using Zaomeng.Equipment;
using Zaomeng.Inventory;
using Zaomeng.Save;

namespace Zaomeng.Items;

/// <summary>先准备所有改动，再一次提交，避免售到一半才发现余额溢出。</summary>
public sealed class BulkEquipmentSale(InventoryService inventory, ItemCatalog catalog, Wallet wallet)
{
	public ItemActionResult Execute()
	{
		var next = inventory.Slots.ToArray();
		long proceeds = 0;
		int count = 0;
		try
		{
			for (int i = 0; i < next.Length; i++)
			{
				var stack = next[i];
				if (stack?.Equipment is not { } instance ||
					!catalog.TryGetDefinition(stack.ItemId, out var item) ||
					item is not EquipmentDefinition { Rarity: EquipmentRarity.Common, SellPrice: > 0 } ||
					instance.SocketedGemIds.Any(id => id.Length > 0)) continue;
				proceeds = checked(proceeds + item.SellPrice);
				count++;
				next[i] = null;
			}
			if (count == 0) return new(false, "没有可出售的白装（镶嵌宝石的装备会保留）。");
			long balance = checked(wallet.Souls + proceeds);
			if (wallet.Souls < 0) return new(false, "灵魂余额无效。");
			inventory.CommitSlots(next, () => wallet.Souls = balance);
			return new(true, $"已出售 {count} 件白装，获得 {proceeds} 灵魂。", true);
		}
		catch (OverflowException) { return new(false, "灵魂余额已达到上限。"); }
	}
}
