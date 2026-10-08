using System;
using Godot;

namespace Zaomeng.Combat.Effects;

/// <summary>本次攻击的不可变快照，公共资源与后续施法不能修改已发出的弹体。</summary>
public sealed record AttackParameters
{
	public static AttackParameters Default { get; } = new();
	public float AttackSpeed { get; init; } = 1;
	public float Range { get; init; } = 1;
	public float VisualScale { get; init; } = 1;
	public float ProjectileSpeed { get; init; } = 1;
	public float ProjectileLifetime { get; init; } = 1;
	public float EffectSpeed { get; init; } = 1;
	public float EffectLifetime { get; init; } = 1;
	public float MotionSpeed { get; init; } = 1;
	public PackedScene? ProjectileScene { get; init; }
	public PackedScene? EffectScene { get; init; }
	public void Validate()
	{
		foreach (float value in new[] { AttackSpeed, Range, VisualScale, ProjectileSpeed, ProjectileLifetime, EffectSpeed, EffectLifetime, MotionSpeed })
			if (!float.IsFinite(value) || value <= 0) throw new InvalidOperationException("攻击参数倍率必须有限且大于零。");
	}
}
