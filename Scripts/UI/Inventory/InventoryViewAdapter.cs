using System;
using System.Collections.Generic;
using Zaomeng.Inventory;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

public sealed record InventoryDisplayEntry(int SlotIndex, ItemDefinition Definition, int Count);

/// <summary>把背包的格子快照解析为 UI 数据，不让格子视图了解背包或物品规则。</summary>
public sealed class InventoryViewAdapter : IDisposable
{
	private readonly IInventoryService _inventory;
	private readonly ItemCatalog _catalog;

	public InventoryViewAdapter(IInventoryService inventory, ItemCatalog catalog)
	{
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
		_catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
		_inventory.Changed += OnInventoryChanged;
	}

	public event Action? Changed;

	public IReadOnlyList<InventoryDisplayEntry> GetEntries(ItemCategory category)
	{
		var entries = new List<InventoryDisplayEntry>();
		IReadOnlyList<ItemStack?> slots = _inventory.Slots;
		for (int index = 0; index < slots.Count; index++)
		{
			ItemStack? stack = slots[index];
			if (stack is null || !_catalog.TryGetDefinition(stack.ItemId, out ItemDefinition? definition))
				continue;
			if (definition!.Category == category)
				entries.Add(new InventoryDisplayEntry(index, definition, stack.Count));
		}
		return entries;
	}

	private void OnInventoryChanged() => Changed?.Invoke();

	public void Dispose() => _inventory.Changed -= OnInventoryChanged;
}
