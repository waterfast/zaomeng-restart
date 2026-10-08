using System;
using Godot;
using Zaomeng.Skills;

namespace Zaomeng.Combat.Effects;

[GlobalClass]
[Tool]
public partial class AttackParameterModifiers : Resource
{
	[Export] public float DamageMultiplier { get; set; } = 1;
	[Export] public float AttackSpeedMultiplier { get; set; } = 1;
	[Export] public float RangeMultiplier { get; set; } = 1;
	[Export] public float VisualScaleMultiplier { get; set; } = 1;
	[Export] public float ProjectileSpeedMultiplier { get; set; } = 1;
	[Export] public float ProjectileLifetimeMultiplier { get; set; } = 1;
	[Export] public float EffectSpeedMultiplier { get; set; } = 1;
	[Export] public float EffectLifetimeMultiplier { get; set; } = 1;
	[Export] public float MotionSpeedMultiplier { get; set; } = 1;
	[Export] public PackedScene? ProjectileSceneOverride { get; set; }
	[Export] public PackedScene? EffectSceneOverride { get; set; }
	public AttackParameters Apply(AttackParameters value) => value with
	{
		AttackSpeed = value.AttackSpeed * AttackSpeedMultiplier,
		Range = value.Range * RangeMultiplier,
		VisualScale = value.VisualScale * VisualScaleMultiplier,
		ProjectileSpeed = value.ProjectileSpeed * ProjectileSpeedMultiplier,
		ProjectileLifetime = value.ProjectileLifetime * ProjectileLifetimeMultiplier,
		EffectSpeed = value.EffectSpeed * EffectSpeedMultiplier,
		EffectLifetime = value.EffectLifetime * EffectLifetimeMultiplier,
		MotionSpeed = value.MotionSpeed * MotionSpeedMultiplier,
		ProjectileScene = ProjectileSceneOverride ?? value.ProjectileScene,
		EffectScene = EffectSceneOverride ?? value.EffectScene
	};
	public void Validate()
	{
		if (!float.IsFinite(DamageMultiplier) || DamageMultiplier < 0)
			throw new InvalidOperationException("伤害倍率必须有限且非负。");
		Apply(AttackParameters.Default).Validate();
		ValidateScene(ProjectileSceneOverride, true);
		ValidateScene(EffectSceneOverride, false);
	}
	private static void ValidateScene(PackedScene? scene, bool projectile)
	{
		if (scene is null) return;
		Node root = scene.Instantiate();
		try
		{
			if (root is not Node2D || (projectile && root is not SkillProjectile))
				throw new InvalidOperationException("替换场景必须是二维特效或指定类型的弹体。");
			if (root is SkillProjectile configured) configured.ValidateConfiguration();
		}
		finally { root.Free(); }
	}
}
