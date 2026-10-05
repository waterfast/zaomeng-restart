using System;
using System.Collections.Generic;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Equipment.Skills;

/// <summary>按实际穿戴重建授予集合；不订阅事件，不累计到已学技能，不持有共享效果状态。</summary>
public sealed class EquipmentSkillRuntime
{
	private IReadOnlyList<EquipmentSkillDefinition> _skills = Array.Empty<EquipmentSkillDefinition>();
	public IReadOnlyList<EquipmentSkillDefinition> Skills => _skills;

	public void Refresh(SaveCharacter character, ItemCatalog catalog)
	{
		var byId = new Dictionary<string, EquipmentSkillDefinition>(StringComparer.Ordinal);
		foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
		{
			EquipmentInstance? instance = character.Equipment.Get(slot);
			if (instance is null) continue;
			catalog.ValidateEquipmentInstance(instance, slot);
			catalog.TryGetDefinition(instance.DefinitionId, out ItemDefinition? item);
			foreach (EquipmentSkillDefinition skill in ((EquipmentDefinition)item!).GrantedSkills)
			{
				if (skill is null) throw new InvalidOperationException("装备授予列表包含空技能。");
				skill.Validate();
				if (byId.TryGetValue(skill.Id, out EquipmentSkillDefinition? existing) && existing != skill)
					throw new InvalidOperationException($"装备技能 ID {skill.Id} 对应不同定义。");
				byId[skill.Id] = skill;
			}
		}
		// 完整验证后一次替换，换装或重复刷新不会残留旧效果，也不会叠加同名效果。
		_skills = new List<EquipmentSkillDefinition>(byId.Values).AsReadOnly();
	}

	public void OnHitDealt(Player owner, CharacterActor target, HitResult hit)
	{
		foreach (EquipmentSkillDefinition skill in _skills) skill.Effect.OnHitDealt(owner, target, hit);
	}

	public float ModifyOutgoingDamage(Player owner, CharacterActor target, SkillDefinition? sourceSkill, float damage)
	{
		foreach (EquipmentSkillDefinition skill in _skills)
		{
			damage = skill.Effect.ModifyOutgoingDamage(owner, target, sourceSkill, damage);
			if (!float.IsFinite(damage) || damage < 0)
				throw new InvalidOperationException($"装备技能 {skill.Id} 返回了无效伤害。");
		}
		return damage;
	}
}
