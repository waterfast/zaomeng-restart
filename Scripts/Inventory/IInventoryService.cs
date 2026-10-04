using System;
using System.Collections.Generic;

namespace Zaomeng.Inventory;

/// <summary>背包的游戏侧接口。失败时不修改任何格子；索引从零开始。</summary>
public interface IInventoryService
{
	int Capacity { get; }
	IReadOnlyList<ItemStack?> Slots { get; }
	event Action? Changed;

	bool AddItem(string itemId, int amount);
	bool RemoveItem(string itemId, int amount);
	bool RemoveEquipment(string instanceId);
	int GetItemCount(string itemId);

	/// <summary>移动指定数量。目标为空时可拆堆，同种物品时可合堆；不自动交换异种物品。</summary>
	bool MoveStack(int sourceIndex, int targetIndex, int amount);
}
