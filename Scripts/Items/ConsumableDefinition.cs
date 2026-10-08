using Godot;

namespace Zaomeng.Items;

public enum ConsumableEffect { GrantSouls, RandomMaterialChest, ApplyBuff }

/// <summary>消耗效果由资源配置；宝箱只引用已迁入的奖励定义，不在界面硬编码物品 ID。</summary>
[GlobalClass]
[Tool]
public partial class ConsumableDefinition : ItemDefinition
{
	[Export] public ConsumableEffect Effect { get; set; }
	[Export] public Zaomeng.Combat.Buffs.BuffDefinition? Buff { get; set; }
	[Export] public long SoulsGranted { get; set; }
	[Export] public string[] RewardItemIds { get; set; } = [];
	[Export] public int RewardMinCount { get; set; } = 1;
	[Export] public int RewardMaxCount { get; set; } = 1;
	public ConsumableDefinition() { Category = ItemCategory.Consumable; MaxStack = 99; }
}
