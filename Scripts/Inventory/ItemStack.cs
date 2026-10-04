using Zaomeng.Equipment;

namespace Zaomeng.Inventory;

/// <summary>装备占一格并携带实例；其他物品按定义 ID 堆叠。快照不可变。</summary>
public sealed record ItemStack(string ItemId, int Count, EquipmentInstance? Equipment = null);
