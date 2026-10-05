using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Items;
namespace Zaomeng.Quests;

[GlobalClass]
public partial class QuestCatalog : Resource
{
	[Export] public Godot.Collections.Array<QuestDefinition> Definitions { get; set; } = new();
	public void Validate(ItemCatalog items)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var quest in Definitions)
		{
			if (quest is null || string.IsNullOrWhiteSpace(quest.Id) || !ids.Add(quest.Id) ||
				string.IsNullOrWhiteSpace(quest.NameKey) || string.IsNullOrWhiteSpace(quest.DescriptionKey) || quest.RequiredLevel < 1 || quest.Rewards.Count == 0)
				throw new InvalidOperationException("任务目录包含空、重复或无效定义。");
			foreach (var reward in quest.Rewards)
				if (reward is null || reward.Count < 1 || !items.TryGetDefinition(reward.ItemId, out _))
					throw new InvalidOperationException($"任务 {quest.Id} 奖励未注册或数量无效。");
		}
	}
}
