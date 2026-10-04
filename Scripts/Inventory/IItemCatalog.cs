namespace Zaomeng.Inventory;

/// <summary>
/// 从物品定义读取堆叠、装备孔位及宝石类型，背包不依赖引擎资源。
/// </summary>
public interface IItemCatalog
{
	bool TryGetMaxStack(string itemId, out int maxStack);
	bool TryGetEquipmentSocketCount(string itemId, out int socketCount);
	bool IsGem(string itemId);
}
