namespace Zaomeng.Equipment;

public enum EquipmentAction { Equip, Unequip }

/// <summary>格子索引和预期实例一起传入，即使同名装备换位也不会误穿。</summary>
public sealed record EquipmentRequest(EquipmentAction Action, int InventorySlot = -1,
	string ExpectedInstanceId = "", EquipmentSlot Slot = EquipmentSlot.Weapon);

public enum EquipmentError
{
	None, ServiceUnavailable, Busy, InvalidSlot, ItemChanged, NotEquipment,
	LevelTooLow, WrongCharacter, InventoryFull, InstanceMissing, InvalidSocket,
	SocketOccupied, GemChanged, NotGem, EmptySocket
}

public sealed record EquipmentChange(string CharacterId, EquipmentSlot Slot,
	EquipmentInstance? Previous, EquipmentInstance? Current);

public sealed record EquipmentResult(EquipmentError Error, string Message, EquipmentChange? Change = null)
{
	public bool Success => Error == EquipmentError.None;
	public static EquipmentResult Fail(EquipmentError error, string message) => new(error, message);
	public static EquipmentResult Unavailable() => Fail(EquipmentError.ServiceUnavailable, "装备服务尚未连接。");
	public static EquipmentResult Busy() => Fail(EquipmentError.Busy, "正在处理装备操作，请稍后再试。");
}
