using System;
using System.Globalization;
using Godot;

namespace Zaomeng.Combat.Effects;

[GlobalClass]
[Tool]
public partial class CriticalResourceRecoveryEffect : PassiveEffect
{
	[Export(PropertyHint.Range, "0,1,0.01")] public float HealthFraction { get; set; }
	[Export(PropertyHint.Range, "0,1,0.01")] public float ManaFraction { get; set; }

	public override void Validate()
	{
		if (!float.IsFinite(HealthFraction) || HealthFraction is < 0 or > 1 ||
			!float.IsFinite(ManaFraction) || ManaFraction is < 0 or > 1)
			throw new InvalidOperationException("暴击恢复的生命与魔法比例必须在 0～1 之间。");
	}

	public override void OnHitDealt(CharacterActor owner, CharacterActor target, HitResult hit)
	{
		if (!hit.Critical || hit.Damage <= 0 || owner.IsDead) return;
		owner.Heal(owner.MaxHealth * HealthFraction);
		if (owner is Player player) player.RestoreMana(player.MaxMana * ManaFraction);
	}

	public override string FormatDescription(string template) => string.Format(CultureInfo.CurrentCulture,
		template, (HealthFraction * 100).ToString("0.##", CultureInfo.CurrentCulture),
		(ManaFraction * 100).ToString("0.##", CultureInfo.CurrentCulture));
}
