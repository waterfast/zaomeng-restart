namespace Zaomeng.Inventory;

/// <summary>一个逻辑格子的只读快照；物品名称、图标和效果由物品定义提供。</summary>
public sealed record ItemStack(string ItemId, int Count);
