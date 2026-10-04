using System;
using Zaomeng.Inventory;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Equipment;

/// <summary>唯一的穿戴规则入口；所有检查完成后才一起提交背包和穿戴槽。</summary>
public sealed class EquipmentService
{
	public SaveCharacter Character { get; }
	private readonly ItemCatalog _catalog;
	private readonly InventoryService _inventory;
	private bool _executing;

	public EquipmentService(SaveCharacter character, ItemCatalog catalog, InventoryService inventory)
	{
		Character = character ?? throw new ArgumentNullException(nameof(character));
		_catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
	}

	public EquipmentResult Execute(EquipmentRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (_executing) return EquipmentResult.Busy();
		_executing = true;
		try
		{
			return request.Action switch
			{
				EquipmentAction.Equip => Equip(request),
				EquipmentAction.Unequip => Unequip(request),
				_ => EquipmentResult.Fail(EquipmentError.InvalidSlot, "无效的装备操作。")
			};
		}
		finally { _executing = false; }
	}

	private EquipmentResult Equip(EquipmentRequest request)
	{
		if (request.InventorySlot < 0 || request.InventorySlot >= _inventory.Capacity)
			return EquipmentResult.Fail(EquipmentError.InvalidSlot, "无效的背包格子。");
		ItemStack? stack = _inventory.Slots[request.InventorySlot];
		if (stack is null)
			return EquipmentResult.Fail(EquipmentError.ItemChanged, "该格子的物品已发生变化，请重新选择。");
		if (stack.Equipment is not EquipmentInstance instance)
			return EquipmentResult.Fail(EquipmentError.NotEquipment, "该物品不能穿戴。");
		if (instance.InstanceId != request.ExpectedInstanceId)
			return EquipmentResult.Fail(EquipmentError.ItemChanged, "该格子的装备已发生变化，请重新选择。");
		if (!_catalog.TryGetDefinition(stack.ItemId, out ItemDefinition? item) ||
			item is not EquipmentDefinition equipment || equipment.Category != ItemCategory.Equipment ||
			!Enum.IsDefined(equipment.Slot))
			return EquipmentResult.Fail(EquipmentError.NotEquipment, "该物品不能穿戴。");
		if (Character.Level < equipment.RequiredLevel)
			return EquipmentResult.Fail(EquipmentError.LevelTooLow, $"需要达到 {equipment.RequiredLevel} 级。");
		if (!equipment.CanEquip(Character))
			return EquipmentResult.Fail(EquipmentError.WrongCharacter, "当前角色不能穿戴这件装备。");
		return Change(equipment.Slot, instance, request.InventorySlot);
	}

	private EquipmentResult Unequip(EquipmentRequest request)
	{
		EquipmentSlot slot = request.Slot;
		if (!Enum.IsDefined(slot))
			return EquipmentResult.Fail(EquipmentError.InvalidSlot, "无效的装备槽位。");
		EquipmentInstance? current = Character.Equipment.Get(slot);
		if (request.ExpectedInstanceId.Length > 0 && current?.InstanceId != request.ExpectedInstanceId)
			return EquipmentResult.Fail(EquipmentError.ItemChanged, "该槽位的装备已发生变化，请重新选择。");
		if (current is null)
			return new(EquipmentError.None, "该槽位没有装备。");
		return Change(slot, null, null);
	}

	private EquipmentResult Change(EquipmentSlot slot, EquipmentInstance? incoming, int? sourceSlot)
	{
		EquipmentInstance? previous = Character.Equipment.Get(slot);
		if (!_inventory.TryExchangeEquipment(sourceSlot, previous,
			() => Character.Equipment.Set(slot, incoming)))
			return EquipmentResult.Fail(EquipmentError.InventoryFull, "背包没有空间容纳卸下的装备。");
		return new(EquipmentError.None, "", new(Character.Id, slot, previous, incoming));
	}
}
