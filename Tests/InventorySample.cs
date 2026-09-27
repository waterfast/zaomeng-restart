using System;
using Zaomeng.Inventory;
using Zaomeng.Items;

namespace Zaomeng;

/// <summary>两个测试场景共用的初始背包；物品内容由场景引用的目录资源决定。</summary>
public static class InventorySample
{
	public static InventoryService Create(ItemCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(catalog);
		catalog.Validate();
		if (catalog.Definitions.Count == 0)
			throw new InvalidOperationException("测试物品目录不能为空。");
		var inventory = new InventoryService(70, catalog);
		foreach (ItemDefinition definition in catalog.Definitions)
		{
			if (!inventory.AddItem(definition.Id, 1))
				throw new InvalidOperationException($"测试背包无法加入物品：{definition.Id}");
		}
		return inventory;
	}
}
