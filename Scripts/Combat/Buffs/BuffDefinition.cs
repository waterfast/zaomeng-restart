using System;
using Godot;

namespace Zaomeng.Combat.Buffs;

[GlobalClass]
[Tool]
public partial class BuffDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string Description { get; set; } = "";
	[Export] public Texture2D? Icon { get; set; }
	[Export] public float Duration { get; set; } = 10;
	[Export] public float ExperienceBonus { get; set; }
	[Export] public float SoulBonus { get; set; }
	[Export] public float HasteBonus { get; set; }
	[Export] public float OutgoingDamageBonus { get; set; }
	[Export] public float FinalDamageMultiplier { get; set; } = 1;
	[Export] public bool SuperArmor { get; set; }
	[Export] public float DamageReduction { get; set; }
	[Export] public float HealRatioPerPulse { get; set; }
	[Export] public float HealFlatPerPulse { get; set; }
	[Export] public float DamagePerPulse { get; set; }
	[Export] public float DamageRatioPerPulse { get; set; }
	[Export] public DamageType PulseDamageType { get; set; } = DamageType.True;
	[Export] public float MoveSpeedMultiplier { get; set; } = 1;
	[Export] public bool PreventsActions { get; set; }
	[Export] public StringName ForcedAnimation { get; set; } = "";
	[Export] public float PulseInterval { get; set; } = 1;
	[Export] public bool PulseImmediately { get; set; }
	[Export] public HitDefinition? PulseHit { get; set; }
	[Export] public float PulseRadius { get; set; } = 150;
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName))
			throw new InvalidOperationException("Buff 必须配置 ID 和名称。");
		foreach (float value in new[] { Duration, ExperienceBonus, SoulBonus, HasteBonus, OutgoingDamageBonus,
			DamageReduction, HealRatioPerPulse, HealFlatPerPulse, PulseInterval, PulseRadius, DamagePerPulse, DamageRatioPerPulse, MoveSpeedMultiplier, FinalDamageMultiplier })
			if (!float.IsFinite(value) || value < 0) throw new InvalidOperationException($"Buff {Id} 数值无效。");
		if (DamageReduction > 1 || HealRatioPerPulse > 1 || PulseInterval <= 0 || PulseRadius <= 0)
			throw new InvalidOperationException($"Buff {Id} 比例或脉冲配置无效。");
	}
}
