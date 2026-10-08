using System;
using System.Collections.Generic;
using Zaomeng.Items;
using Zaomeng.Equipment;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Combat.Effects;

/// <summary>按实际穿戴重建授予集合；不订阅事件，不累计到已学技能，不持有共享效果状态。</summary>
public static class EquipmentPassiveSkills
{
	public static IReadOnlyList<PassiveSkillDefinition> Resolve(SaveCharacter character, ItemCatalog catalog)
	{
		var byId = new Dictionary<string, PassiveSkillDefinition>(StringComparer.Ordinal);
		foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
		{
			EquipmentInstance? instance = character.Equipment.Get(slot);
			if (instance is null) continue;
			catalog.ValidateEquipmentInstance(instance, slot);
			catalog.TryGetDefinition(instance.DefinitionId, out ItemDefinition? item);
			foreach (PassiveSkillDefinition skill in ((EquipmentDefinition)item!).GrantedSkills)
			{
				if (skill is null) throw new InvalidOperationException("装备授予列表包含空技能。");
				skill.Validate();
				if (byId.TryGetValue(skill.Id, out PassiveSkillDefinition? existing) && existing != skill)
					throw new InvalidOperationException($"装备技能 ID {skill.Id} 对应不同定义。");
				byId[skill.Id] = skill;
			}
		}
		// 完整验证后一次替换，换装或重复刷新不会残留旧效果，也不会叠加同名效果。
		return new List<PassiveSkillDefinition>(byId.Values).AsReadOnly();
	}

}
