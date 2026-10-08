using System;
using System.Globalization;
using Godot;

namespace Zaomeng.Combat.Effects;

/// <summary>目标筛选、条件和参数都在资源配置，同类效果无需新增角色分支。</summary>
[GlobalClass]
[Tool]
public partial class SkillParameterEffect : PassiveEffect
{
	[Export] public Godot.Collections.Array<SkillDefinition> TargetSkills { get; set; } = new();
	[Export] public bool AffectsNormalAttacks { get; set; } = true;
	[Export] public PassiveCondition? Condition { get; set; }
	[Export] public AttackParameterModifiers Modifiers { get; set; } = new();
	public override void Validate()
	{
		if (Modifiers is null) throw new InvalidOperationException("参数效果缺少修改配置。");
		foreach (var skill in TargetSkills)
			if (skill is null) throw new InvalidOperationException("参数效果的目标技能不能为空。");
		Condition?.Validate();
		Modifiers.Validate();
	}
	private bool Matches(CharacterActor owner, SkillDefinition? skill) =>
		(skill is null ? AffectsNormalAttacks : TargetSkills.Count == 0 || TargetSkills.Contains(skill)) &&
		(Condition?.Matches(owner) ?? !owner.IsDead);
	public override AttackParameters ModifyAttackParameters(CharacterActor owner, SkillDefinition? skill, AttackParameters parameters)
		=> Matches(owner, skill) ? Modifiers.Apply(parameters) : parameters;
	public override float ModifyOutgoingDamage(CharacterActor owner, CharacterActor target, SkillDefinition? skill, float damage)
		=> Matches(owner, skill) ? damage * Modifiers.DamageMultiplier : damage;
	public override string FormatDescription(string template) => string.Format(CultureInfo.CurrentCulture, template,
		((Modifiers.DamageMultiplier - 1) * 100).ToString("0.##", CultureInfo.CurrentCulture),
		((Modifiers.AttackSpeedMultiplier - 1) * 100).ToString("0.##", CultureInfo.CurrentCulture),
		((Modifiers.RangeMultiplier - 1) * 100).ToString("0.##", CultureInfo.CurrentCulture),
		((Condition as HealthRatioCondition)?.BelowRatio * 100 ?? 0).ToString("0.##", CultureInfo.CurrentCulture));
}
