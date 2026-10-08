using System;
using Godot;
using Zaomeng.Skills;

namespace Zaomeng.Equipment;

[GlobalClass]
[Tool]
public partial class MagicWeaponAbility : Resource
{
	[Export] public SkillDefinition Action { get; set; } = null!;
	[Export] public BurstAttackDefinition Burst { get; set; } = null!;
	public void Validate()
	{
		if (Action is null || string.IsNullOrWhiteSpace(Action.Id) || Action.Growth is null || Burst is null ||
			!float.IsFinite(Action.CooldownSeconds) || Action.CooldownSeconds <= 0)
			throw new InvalidOperationException("法宝主动缺少动作、魔耗、冷却或爆发配置。");
		Action.Growth.Validate(1);
		Burst.Validate();
	}
}
