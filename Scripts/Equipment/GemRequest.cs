namespace Zaomeng.Equipment;

public enum GemAction { Socket, Remove }

/// <summary>目标始终是独立装备，宝石来源/拆卸预期也必须校验。</summary>
public sealed record GemRequest(GemAction Action, string EquipmentInstanceId, int SocketIndex,
	int GemInventorySlot = -1, string ExpectedGemId = "");

public sealed record EquipmentModification(string CharacterId, EquipmentInstance Previous, EquipmentInstance Current);

public sealed record GemResult(EquipmentError Error, string Message, EquipmentModification? Change = null)
{
	public bool Success => Error == EquipmentError.None;
	public static GemResult Fail(EquipmentError error, string message) => new(error, message);
	public static GemResult Unavailable() => Fail(EquipmentError.ServiceUnavailable, "宝石服务尚未连接。");
	public static GemResult Busy() => Fail(EquipmentError.Busy, "正在处理装备操作，请稍后再试。");
}
