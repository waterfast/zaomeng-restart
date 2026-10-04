using System.Collections.Generic;
using Zaomeng.Equipment;

namespace Zaomeng.Save;

/// <summary>只保存跨关卡持久状态；当前关卡、位置和血量由每次进关重新初始化。</summary>
public sealed class GameSaveData
{
	public const int CurrentVersion = 2;
	public int Version { get; set; } = CurrentVersion;
	public InventorySaveData Inventory { get; set; } = new();
	public Wallet Wallet { get; set; } = new();
	public List<Zaomeng.Character.Character> Characters { get; set; } = new();
	// 新游戏只开放第一关，通关时更新解锁进度。
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
	public EquipmentInstance? Equipment { get; set; }
}
