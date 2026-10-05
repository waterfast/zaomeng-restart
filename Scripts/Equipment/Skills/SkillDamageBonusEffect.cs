using System;
using System.Globalization;
using Godot;

namespace Zaomeng.Equipment.Skills;

/// <summary>通过资源引用选择技能，不向角色或目标技能添加装备分支。</summary>
[GlobalClass]
public partial class SkillDamageBonusEffect : EquipmentSkillEffect
{
	[Export] public SkillDefinition TargetSkill { get; set; } = null!;
	[Export] public float BonusFraction { get; set; } = 0.5f;
	public override void Validate()
	{
		if (TargetSkill is null || !float.IsFinite(BonusFraction) || BonusFraction < 0)
			throw new InvalidOperationException("技能增伤效果必须引用技能并提供有限的非负比例。");
	}
	public override float ModifyOutgoingDamage(Player owner, CharacterActor target, SkillDefinition? sourceSkill, float damage)
		=> sourceSkill == TargetSkill ? damage * (1 + BonusFraction) : damage;
	public override string FormatDescription(string template) => string.Format(CultureInfo.CurrentCulture,
		template, (BonusFraction * 100).ToString("0.##", CultureInfo.CurrentCulture));
}
