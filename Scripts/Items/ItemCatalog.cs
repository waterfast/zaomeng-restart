using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Inventory;

namespace Zaomeng.Items;

/// <summary>物品定义的唯一目录，同时把最大堆叠数提供给纯 C# 背包。</summary>
[GlobalClass]
[Tool]
public partial class ItemCatalog : Resource, IItemCatalog
{
	[Export] public Godot.Collections.Array<ItemDefinition> Definitions { get; set; } = new();

	public bool TryGetDefinition(string itemId, out ItemDefinition? definition)
	{
		foreach (ItemDefinition? candidate in Definitions)
		{
			if (candidate?.Id == itemId)
			{
				definition = candidate;
				return true;
			}
		}
		definition = null;
		return false;
	}

	public bool TryGetMaxStack(string itemId, out int maxStack)
	{
		if (TryGetDefinition(itemId, out ItemDefinition? definition))
		{
			maxStack = definition!.MaxStack;
			return true;
		}
		maxStack = 0;
		return false;
	}

	public void Validate()
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemDefinition? definition in Definitions)
		{
			if (definition is null)
				throw new InvalidOperationException("物品目录包含空定义。");
			if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id))
				throw new InvalidOperationException($"物品 ID 为空或重复：{definition.Id}");
			if (definition.MaxStack < 1)
				throw new InvalidOperationException($"物品 {definition.Id} 的最大堆叠数必须大于零。");
		}
	}
}
