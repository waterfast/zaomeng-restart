namespace Zaomeng.Inventory;

/// <summary>
/// 从唯一的物品定义来源读取背包所需的堆叠规则。后续可由 Expresso 的物品数据库实现。
/// </summary>
public interface IItemCatalog
{
	bool TryGetMaxStack(string itemId, out int maxStack);
}
