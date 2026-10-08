using System;
using Godot;

namespace Zaomeng.Combat.Effects;

[GlobalClass]
[Tool]
public partial class HealthRatioCondition : PassiveCondition
{
	[Export(PropertyHint.Range, "0,1,0.01")] public float BelowRatio { get; set; } = 0.2f;
	public override bool Matches(CharacterActor owner) => !owner.IsDead && owner.MaxHealth > 0 && owner.Health < owner.MaxHealth * BelowRatio;
	public override void Validate()
	{
		if (!float.IsFinite(BelowRatio) || BelowRatio <= 0 || BelowRatio > 1)
			throw new InvalidOperationException("血量阈值必须大于零且不超过 100%。");
	}
}
