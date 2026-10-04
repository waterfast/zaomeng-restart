using System;
using System.Linq;
using Zaomeng.Inventory;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Equipment;

/// <summary>镶嵌消耗一颗宝石，拆卸归还一颗；先检查副本，失败不消耗物品。</summary>
public sealed class GemSocketService(SaveCharacter character, ItemCatalog catalog, InventoryService inventory)
{
	private bool _executing;

	public GemResult Execute(GemRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (_executing) return GemResult.Busy();
		_executing = true;
		try { return Change(request); }
		finally { _executing = false; }
	}

	private GemResult Change(GemRequest request)
	{
		var next = inventory.Slots.ToArray();
		int bagSlot = Array.FindIndex(next, stack => stack?.Equipment?.InstanceId == request.EquipmentInstanceId);
		EquipmentSlot? equippedSlot = null;
		EquipmentInstance? previous = bagSlot >= 0 ? next[bagSlot]!.Equipment : null;
		if (previous is null)
			foreach (var (slot, instance) in character.Equipment.Slots)
				if (instance.InstanceId == request.EquipmentInstanceId) { previous = instance; equippedSlot = slot; break; }
		if (previous is null)
			return GemResult.Fail(EquipmentError.InstanceMissing, "当前背包或角色没有这件装备。");
		catalog.ValidateEquipmentInstance(previous, equippedSlot);
		if (request.SocketIndex < 0 || request.SocketIndex >= previous.SocketedGemIds.Length)
			return GemResult.Fail(EquipmentError.InvalidSocket, "无效的宝石孔位。");
		string oldGem = previous.SocketedGemIds[request.SocketIndex];
		string newGem;
		if (request.Action == GemAction.Socket)
		{
			if (oldGem.Length > 0)
				return GemResult.Fail(EquipmentError.SocketOccupied, "此孔位已有宝石，请先拆卸。");
			if (request.GemInventorySlot < 0 || request.GemInventorySlot >= next.Length)
				return GemResult.Fail(EquipmentError.InvalidSlot, "无效的宝石来源格子。");
			ItemStack? gem = next[request.GemInventorySlot];
			if (gem is null || gem.ItemId != request.ExpectedGemId)
				return GemResult.Fail(EquipmentError.GemChanged, "选中的宝石已发生变化。");
			if (!catalog.IsGem(gem.ItemId))
				return GemResult.Fail(EquipmentError.NotGem, "选中的物品不是宝石。");
			newGem = gem.ItemId;
			next[request.GemInventorySlot] = gem.Count == 1 ? null : gem with { Count = gem.Count - 1 };
		}
		else if (request.Action == GemAction.Remove)
		{
			if (oldGem.Length == 0) return GemResult.Fail(EquipmentError.EmptySocket, "此孔位没有宝石。");
			if (oldGem != request.ExpectedGemId)
				return GemResult.Fail(EquipmentError.GemChanged, "孔位中的宝石已发生变化。");
			if (!inventory.TryAddStackable(next, oldGem))
				return GemResult.Fail(EquipmentError.InventoryFull, "背包没有空间容纳拆下的宝石。");
			newGem = "";
		}
		else return GemResult.Fail(EquipmentError.InvalidSocket, "无效的宝石操作。");

		EquipmentInstance current = previous.WithGem(request.SocketIndex, newGem);
		if (bagSlot >= 0) next[bagSlot] = next[bagSlot]! with { Equipment = current };
		inventory.CommitSlots(next, () =>
		{
			if (equippedSlot is EquipmentSlot slot) character.Equipment.Set(slot, current);
		});
		return new(EquipmentError.None, "", new(character.Id, previous, current));
	}
}
