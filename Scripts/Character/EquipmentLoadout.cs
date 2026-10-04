using System;
using System.Collections.Immutable;
using Zaomeng.Equipment;

namespace Zaomeng.Character;

/// <summary>每个穿戴槽拥有一个完整装备实例；空槽不存条目。</summary>
public sealed class EquipmentLoadout
{
	public ImmutableDictionary<EquipmentSlot, EquipmentInstance> Slots { get; set; } =
		ImmutableDictionary<EquipmentSlot, EquipmentInstance>.Empty;

	public EquipmentInstance? Get(EquipmentSlot slot)
	{
		if (!Enum.IsDefined(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
		return Slots.GetValueOrDefault(slot);
	}

	internal void Set(EquipmentSlot slot, EquipmentInstance? instance)
	{
		if (!Enum.IsDefined(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
		Slots = instance is null ? Slots.Remove(slot) : Slots.SetItem(slot, instance);
	}
}
