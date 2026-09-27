using System;
using System.Collections.Generic;

namespace Zaomeng.Inventory;

/// <summary>独立于 Godot 的背包逻辑实现，供接口与规则测试。</summary>
public sealed class InventoryService : IInventoryService
{
	private readonly IItemCatalog _catalog;
	private readonly ItemStack?[] _slots;

	public InventoryService(int capacity, IItemCatalog catalog)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		_catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
		_slots = new ItemStack?[capacity];
	}

	public int Capacity => _slots.Length;

	// 复制快照，避免调用方绕过背包规则直接修改槽位。
	public IReadOnlyList<ItemStack?> Slots => Array.AsReadOnly((ItemStack?[])_slots.Clone());

	public event Action? Changed;

	/// <summary>一次性恢复全部槽位；先校验再修改，失败时保留原背包。</summary>
	public void RestoreSlots(IReadOnlyList<ItemStack?> slots)
	{
		ItemStack?[] restored = CreateValidatedSnapshot(slots);
		Array.Copy(restored, _slots, Capacity);
		Changed?.Invoke();
	}

	/// <summary>用于跨系统恢复前检查存档；不改变当前背包。</summary>
	public void ValidateRestoredSlots(IReadOnlyList<ItemStack?> slots) => CreateValidatedSnapshot(slots);

	private ItemStack?[] CreateValidatedSnapshot(IReadOnlyList<ItemStack?> slots)
	{
		ArgumentNullException.ThrowIfNull(slots);
		if (slots.Count != Capacity)
			throw new ArgumentException("存档槽位数与背包容量不一致。", nameof(slots));

		var restored = new ItemStack?[Capacity];
		var totals = new Dictionary<string, long>(StringComparer.Ordinal);
		for (int i = 0; i < slots.Count; i++)
		{
			ItemStack? stack = slots[i];
			if (stack is null) continue;
			if (string.IsNullOrWhiteSpace(stack.ItemId) || stack.Count <= 0 ||
				!TryGetMaxStack(stack.ItemId, out int maxStack) || stack.Count > maxStack)
				throw new ArgumentException($"存档中第 {i} 格的物品无效。", nameof(slots));
			long total = totals.GetValueOrDefault(stack.ItemId) + stack.Count;
			if (total > int.MaxValue)
				throw new ArgumentException($"物品 {stack.ItemId} 的总数量超出背包支持范围。", nameof(slots));
			totals[stack.ItemId] = total;
			restored[i] = stack;
		}

		return restored;
	}

	public bool AddItem(string itemId, int amount)
	{
		ValidateItemRequest(itemId, amount);
		if (!TryGetMaxStack(itemId, out int maxStack))
			return false;
		// 查询接口使用 int，避免格子足够多时存入无法表示的总数。
		if (amount > int.MaxValue - GetItemCount(itemId))
			return false;

		long available = 0;
		foreach (ItemStack? stack in _slots)
		{
			if (stack is null)
				available += maxStack;
			else if (stack.ItemId == itemId)
				available += Math.Max(0, maxStack - stack.Count);
		}
		if (available < amount)
			return false;

		int remaining = amount;
		for (int i = 0; i < _slots.Length && remaining > 0; i++)
		{
			ItemStack? stack = _slots[i];
			if (stack is null || stack.ItemId != itemId)
				continue;
			int added = Math.Min(remaining, maxStack - stack.Count);
			if (added > 0)
			{
				_slots[i] = stack with { Count = stack.Count + added };
				remaining -= added;
			}
		}
		for (int i = 0; i < _slots.Length && remaining > 0; i++)
		{
			if (_slots[i] is not null)
				continue;
			int added = Math.Min(remaining, maxStack);
			_slots[i] = new ItemStack(itemId, added);
			remaining -= added;
		}
		Changed?.Invoke();
		return true;
	}

	public bool RemoveItem(string itemId, int amount)
	{
		ValidateItemRequest(itemId, amount);
		if (GetItemCount(itemId) < amount)
			return false;

		int remaining = amount;
		for (int i = 0; i < _slots.Length && remaining > 0; i++)
		{
			ItemStack? stack = _slots[i];
			if (stack is null || stack.ItemId != itemId)
				continue;
			int removed = Math.Min(remaining, stack.Count);
			int newCount = stack.Count - removed;
			_slots[i] = newCount == 0 ? null : stack with { Count = newCount };
			remaining -= removed;
		}
		Changed?.Invoke();
		return true;
	}

	public int GetItemCount(string itemId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
		long count = 0;
		foreach (ItemStack? stack in _slots)
		{
			if (stack?.ItemId == itemId)
				count += stack.Count;
		}
		return checked((int)count);
	}

	public bool MoveStack(int sourceIndex, int targetIndex, int amount)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
		ValidateIndex(sourceIndex);
		ValidateIndex(targetIndex);
		if (sourceIndex == targetIndex)
			return false;

		ItemStack? source = _slots[sourceIndex];
		ItemStack? target = _slots[targetIndex];
		if (source is null || amount > source.Count ||
			(target is not null && target.ItemId != source.ItemId) ||
			!TryGetMaxStack(source.ItemId, out int maxStack) ||
			amount > maxStack - (target?.Count ?? 0))
			return false;

		_slots[sourceIndex] = amount == source.Count ? null : source with { Count = source.Count - amount };
		_slots[targetIndex] = new ItemStack(source.ItemId, (target?.Count ?? 0) + amount);
		Changed?.Invoke();
		return true;
	}

	private bool TryGetMaxStack(string itemId, out int maxStack)
	{
		if (!_catalog.TryGetMaxStack(itemId, out maxStack))
			return false;
		if (maxStack < 1)
			throw new InvalidOperationException($"物品 {itemId} 的最大堆叠数必须大于零。");
		return true;
	}

	private void ValidateIndex(int index)
	{
		if ((uint)index >= (uint)_slots.Length)
			throw new ArgumentOutOfRangeException(nameof(index));
	}

	private static void ValidateItemRequest(string itemId, int amount)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
	}
}
