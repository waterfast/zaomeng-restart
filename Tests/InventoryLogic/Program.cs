using System;
using System.Collections.Generic;
using Zaomeng.Inventory;

var catalog = new TestCatalog(new Dictionary<string, int>
{
	["herb"] = 10,
	["sword"] = 1,
	["gem"] = 5
});
IInventoryService inventory = new InventoryService(4, catalog);
int changes = 0;
inventory.Changed += () => changes++;

Check(inventory.AddItem("herb", 12), "add spans multiple stacks");
Check(inventory.Slots[0] == new ItemStack("herb", 10) &&
	inventory.Slots[1] == new ItemStack("herb", 2), "stack limit respected");
Check(inventory.AddItem("herb", 3) && inventory.Slots[1]?.Count == 5,
	"partial stack filled first");
Check(inventory.AddItem("sword", 1) && inventory.Slots[2]?.Count == 1,
	"nonstackable item uses one slot");

var beforeFailure = inventory.Slots;
Check(!inventory.AddItem("gem", 6), "insufficient capacity rejects whole addition");
Check(!inventory.AddItem("missing", 1), "unknown item rejected");
Check(SameSlots(beforeFailure, inventory.Slots), "failed additions leave slots untouched");

Check(!inventory.RemoveItem("herb", 16), "insufficient quantity rejects removal");
Check(inventory.GetItemCount("herb") == 15, "failed removal is atomic");
Check(inventory.RemoveItem("herb", 11) && inventory.GetItemCount("herb") == 4,
	"removal spans stacks and clears empty slot");
Check(inventory.Slots[0] is null && inventory.Slots[1]?.Count == 4,
	"empty stack removed");

Check(inventory.MoveStack(1, 3, 2), "move to empty slot splits stack");
Check(inventory.Slots[1]?.Count == 2 && inventory.Slots[3]?.Count == 2,
	"split counts are correct");
Check(inventory.MoveStack(3, 1, 2), "move onto same item merges stacks");
Check(inventory.Slots[3] is null && inventory.Slots[1]?.Count == 4,
	"merge clears source");
Check(!inventory.MoveStack(1, 2, 1), "different items cannot merge");
Check(!inventory.MoveStack(1, 0, 5), "cannot move more than source contains");
Check(!inventory.MoveStack(1, 1, 1), "moving onto itself is a no-op");
Check(inventory.AddItem("herb", 6) && inventory.Slots[1]?.Count == 10,
	"a merged stack can be filled again");
Check(inventory.AddItem("herb", 1) && inventory.Slots[0]?.Count == 1,
	"full stack sends overflow to empty slot");
Check(!inventory.MoveStack(0, 1, 1), "overfull merge rejected");

var snapshot = inventory.Slots;
Check(inventory.RemoveItem("herb", 1), "remove after snapshot");
Check(snapshot[0]?.Count == 1 && inventory.Slots[0] is null,
	"slot snapshots do not change with inventory");
Check(changes == 9, "change event fires once per successful mutation");

IInventoryService largeInventory = new InventoryService(2,
	new TestCatalog(new Dictionary<string, int> { ["large"] = int.MaxValue }));
Check(largeInventory.AddItem("large", int.MaxValue), "largest supported count accepted");
Check(!largeInventory.AddItem("large", 1), "total count cannot overflow query result");
Check(largeInventory.GetItemCount("large") == int.MaxValue,
	"overflow rejection leaves inventory untouched");

ExpectException<ArgumentOutOfRangeException>(() => inventory.AddItem("herb", 0));
ExpectException<ArgumentOutOfRangeException>(() => inventory.MoveStack(-1, 0, 1));
ExpectException<ArgumentException>(() => inventory.RemoveItem(" ", 1));
Console.WriteLine("Inventory logic tests passed.");

static bool SameSlots(IReadOnlyList<ItemStack?> first, IReadOnlyList<ItemStack?> second)
{
	if (first.Count != second.Count)
		return false;
	for (int i = 0; i < first.Count; i++)
	{
		if (first[i] != second[i])
			return false;
	}
	return true;
}

static void Check(bool condition, string description)
{
	if (!condition)
		throw new Exception($"FAILED: {description}");
}

static void ExpectException<T>(Action action) where T : Exception
{
	try
	{
		action();
	}
	catch (T)
	{
		return;
	}
	throw new Exception($"Expected {typeof(T).Name}.");
}

sealed class TestCatalog(IReadOnlyDictionary<string, int> maxStacks) : IItemCatalog
{
	public bool TryGetMaxStack(string itemId, out int maxStack) =>
		maxStacks.TryGetValue(itemId, out maxStack);
}
