using System.Collections.Generic;

namespace Zaomeng.Save;

/// <summary>只保存跨关卡持久状态；当前关卡、位置和血量由每次进关重新初始化。</summary>
public sealed class GameSaveData
{
	public const int CurrentVersion = 1;
	public int Version { get; set; } = CurrentVersion;
	public InventorySaveData Inventory { get; set; } = new();
	public Wallet Wallet { get; set; } = new();
	public List<Zaomeng.Character.Character> Characters { get; set; } = new();
	// 旧版存档没有此字段，反序列化时默认只开放第一关。
	public int UnlockedLevel { get; set; } = 1;
}

public sealed class InventorySaveData
{
	public int Capacity { get; set; }
	// null 表示空槽；保留索引，才能还原玩家整理过的背包顺序。
	public List<ItemStackSaveData?> Slots { get; set; } = new();
}

public sealed class ItemStackSaveData
{
	public string ItemId { get; set; } = "";
	public int Count { get; set; }
}
