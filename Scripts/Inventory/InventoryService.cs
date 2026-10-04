using System;
using System.Collections.Generic;
using Zaomeng.Equipment;

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

	/// <summary>先在副本中交换；commitLoadout 只负责设置穿戴槽，不调用外部监听者。</summary>
	internal bool TryExchangeEquipment(int? sourceSlot, EquipmentInstance? outgoing, Action commitLoadout)
	{
		var next = (ItemStack?[])_slots.Clone();
		if (sourceSlot is int index)
		{
			ValidateIndex(index);
			ItemStack? incoming = next[index];
			if (incoming?.Equipment is null || incoming.Count != 1) return false;
			next[index] = null;
		}
		if (outgoing is not null)
		{
			int target = Array.FindIndex(next, stack => stack is null);
			if (target < 0) return false;
			next[target] = new ItemStack(outgoing.DefinitionId, 1, outgoing);
		}
		CommitSlots(next, commitLoadout);
		return true;
	}

	/// <summary>服务已完成规则检查；再次验证快照后一次性提交，回调只设置角色数据。</summary>
	internal void CommitSlots(IReadOnlyList<ItemStack?> slots, Action commitLoadout)
	{
		ItemStack?[] next = CreateValidatedSnapshot(slots);
		commitLoadout();
		Array.Copy(next, _slots, Capacity);
		PublishChanged();
	}

	internal bool TryAddStackable(ItemStack?[] slots, string itemId)
	{
		if (!TryGetMaxStack(itemId, out int maxStack) || _catalog.TryGetEquipmentSocketCount(itemId, out _)) return false;
		long total = 0;
		foreach (ItemStack? stack in slots) if (stack?.ItemId == itemId) total += stack.Count;
		if (total >= int.MaxValue) return false;
		int target = Array.FindIndex(slots, stack => stack?.ItemId == itemId && stack.Count < maxStack);
		if (target < 0) target = Array.FindIndex(slots, stack => stack is null);
		if (target < 0) return false;
		slots[target] = new ItemStack(itemId, (slots[target]?.Count ?? 0) + 1);
		return true;
	}

	private void PublishChanged()
	{
		if (Changed is null) return;
		// 状态已提交，界面监听异常不能让调用方把成功交换误认为失败。
		foreach (Action listener in Changed.GetInvocationList())
		{
			try { listener(); }
			catch (Exception error) { System.Diagnostics.Trace.TraceError($"背包监听者执行失败：{error}"); }
		}
	}

	/// <summary>一次性恢复全部槽位；先校验再修改，失败时保留原背包。</summary>
	public void RestoreSlots(IReadOnlyList<ItemStack?> slots)
	{
		ItemStack?[] restored = CreateValidatedSnapshot(slots);
		Array.Copy(restored, _slots, Capacity);
		PublishChanged();
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
		var instanceIds = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < slots.Count; i++)
		{
			ItemStack? stack = slots[i];
			if (stack is null) continue;
			if (string.IsNullOrWhiteSpace(stack.ItemId) || stack.Count <= 0 ||
				!TryGetMaxStack(stack.ItemId, out int maxStack) || stack.Count > maxStack)
				throw new ArgumentException($"存档中第 {i} 格的物品无效。", nameof(slots));
			bool isEquipment = _catalog.TryGetEquipmentSocketCount(stack.ItemId, out int socketCount);
			if (isEquipment && (stack.Count != 1 || stack.Equipment is not { IsValid: true } instance ||
				instance.DefinitionId != stack.ItemId || instance.SocketedGemIds.Length != socketCount ||
				!instanceIds.Add(instance.InstanceId)))
				throw new ArgumentException($"存档中第 {i} 格的装备实例无效或重复。", nameof(slots));
			if (!isEquipment && stack.Equipment is not null)
				throw new ArgumentException($"存档中第 {i} 格的普通物品不应包含装备实例。", nameof(slots));
			if (stack.Equipment is not null)
				foreach (string gemId in stack.Equipment.SocketedGemIds)
					if (gemId.Length > 0 && !_catalog.IsGem(gemId))
						throw new ArgumentException($"存档中第 {i} 格引用了未知宝石。", nameof(slots));
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
		bool isEquipment = _catalog.TryGetEquipmentSocketCount(itemId, out int socketCount);
		if (isEquipment && maxStack != 1)
			throw new InvalidOperationException("独立装备的最大堆叠数必须为 1。");
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
			_slots[i] = new ItemStack(itemId, added,
				isEquipment ? EquipmentInstance.Create(itemId, socketCount) : null);
			remaining -= added;
		}
		PublishChanged();
		return true;
	}

	public bool RemoveItem(string itemId, int amount)
	{
		ValidateItemRequest(itemId, amount);
		// 按定义扣数量只用于堆叠物品，不能随机销毁某件带有宝石的装备。
		if (_catalog.TryGetEquipmentSocketCount(itemId, out _)) return false;
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
		PublishChanged();
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

	/// <summary>丢弃/出售等操作按实例定位，不能按定义随机选择某件装备。</summary>
	public bool RemoveEquipment(string instanceId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
		int slot = Array.FindIndex(_slots, stack => stack?.Equipment?.InstanceId == instanceId);
		if (slot < 0) return false;
		_slots[slot] = null;
		PublishChanged();
		return true;
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
		if (source?.Equipment is not null)
		{
			if (amount != 1 || target is not null) return false;
			_slots[sourceIndex] = null;
			_slots[targetIndex] = source;
			PublishChanged();
			return true;
		}
		if (source is null || amount > source.Count ||
			(target is not null && target.ItemId != source.ItemId) ||
			!TryGetMaxStack(source.ItemId, out int maxStack) ||
			amount > maxStack - (target?.Count ?? 0))
			return false;

		_slots[sourceIndex] = amount == source.Count ? null : source with { Count = source.Count - amount };
		_slots[targetIndex] = new ItemStack(source.ItemId, (target?.Count ?? 0) + amount);
		PublishChanged();
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
