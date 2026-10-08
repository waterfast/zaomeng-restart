using System.Collections.Generic;
using System;
using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng.Skills;

/// <summary>脱离施法者后独立运行的弹体，穿过怪物时只对每个目标结算一次。</summary>
public partial class SkillProjectile : Node2D
{
	[Export] public float Speed { get; set; } = 600;
	[Export] public float Lifetime { get; set; } = 1.4f;
	[Export] public float HitRadius { get; set; } = 55;
	[Export] public float VerticalHitRadius { get; set; } = 65;
	[Export] public HitDefinition Hit { get; set; } = null!;
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public StringName Animation { get; set; } = "";
	[Export] public float VisualScale { get; set; } = 1;
	[Export] public float VisualRotationDegrees { get; set; }
	private CharacterActor? _source;
	private int _direction;
	private int _level;
	private int _sourceRevision;
	private SkillDefinition? _sourceSkill;
	private int _wushuangGain;
	private float _elapsed;
	public float TravelAngleDegrees { get; set; }
	public AttackParameters Parameters { get; private set; } = AttackParameters.Default;
	private readonly HashSet<CharacterActor> _hitTargets = new();
	public void ValidateConfiguration()
	{
		if (Hit is null || Frames is null || !Frames.HasAnimation(Animation) || Frames.GetFrameCount(Animation) == 0 ||
			!float.IsFinite(Speed) || Speed <= 0 || !float.IsFinite(Lifetime) || Lifetime <= 0 ||
			!float.IsFinite(HitRadius) || HitRadius <= 0 || !float.IsFinite(VerticalHitRadius) || VerticalHitRadius <= 0 ||
			!float.IsFinite(VisualScale) || VisualScale <= 0 || !float.IsFinite(VisualRotationDegrees))
			throw new InvalidOperationException("弹体缺少命中、动画或有效运动与范围配置。");
	}
	public void Configure(CharacterActor source, int? level = null, SkillDefinition? sourceSkill = null, AttackParameters? parameters = null)
	{
		Parameters = parameters ?? source.CurrentAttackParameters;
		_source = source; _direction = source.FacingDirection; _level = level ?? source.CurrentHitLevel;
		_sourceRevision = source.SpawnRevision;
		_sourceSkill = sourceSkill ?? source.CurrentSkill;
		_wushuangGain = source is Player player ? player.Wushuang.SampleGain(Hit, _sourceSkill)
			: Zaomeng.Combat.Wushuang.WushuangGain.Resolve(Hit, _sourceSkill);
	}
	public override void _Ready()
	{
		if (_source is null)
		{
			for (Node? ancestor = GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
				if (ancestor is CharacterActor player) { Configure(player); break; }
			if (_source is null) { QueueFree(); return; }
			CallDeferred(MethodName.DetachFromActor);
		}
		float scale = VisualScale * Parameters.VisualScale;
		var sprite = new AnimatedSprite2D { SpriteFrames = Frames, Scale = new(-_direction * scale, scale),
			RotationDegrees = VisualRotationDegrees + TravelAngleDegrees * _direction, SpeedScale = Parameters.EffectSpeed };
		AddChild(sprite);
		sprite.Play(Animation);
	}
	private void DetachFromActor()
	{
		if (!IsInstanceValid(_source)) { QueueFree(); return; }
		Reparent(_source!.GetParent(), true);
		// Godot 会将继承的负 X 缩放分解为半周旋转；独立弹体只由内部精灵镜像。
		Rotation = 0;
		Scale = Vector2.One;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_source) || _source!.IsDead || !_source.Visible || _source.SpawnRevision != _sourceRevision)
		{ QueueFree(); return; }
		_elapsed += (float)delta;
		if (_elapsed >= Lifetime * Parameters.ProjectileLifetime) { QueueFree(); return; }
		Vector2 travel = Vector2.Right.Rotated(Mathf.DegToRad(TravelAngleDegrees));
		travel.X *= _direction;
		Vector2 previous = GlobalPosition;
		Vector2 destination = previous + travel * Speed * Parameters.ProjectileSpeed * (float)delta;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, destination, 1);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0) { QueueFree(); return; }
		GlobalPosition = destination;
		foreach (Node node in GetTree().GetNodesInGroup("combat_actors"))
			if (node is CharacterActor { Visible: true } monster && monster.Team != _source.Team && !monster.IsDead && !_hitTargets.Contains(monster) &&
				TouchesTarget(previous, destination, monster.GlobalPosition - new Vector2(0, 35)) && CombatResolver.Resolve(_source, monster, Hit, _level, _sourceSkill, _wushuangGain))
				_hitTargets.Add(monster);
	}

	private bool TouchesTarget(Vector2 from, Vector2 to, Vector2 center)
	{
		// 在两轴上裁剪运动线段，保留原来的矩形范围，同时覆盖高速弹体跨帧穿越。
		float enter = 0, leave = 1;
		Vector2 movement = to - from;
		return ClipAxis(from.X - center.X, movement.X, HitRadius * Parameters.Range, ref enter, ref leave)
			&& ClipAxis(from.Y - center.Y, movement.Y, VerticalHitRadius * Parameters.Range, ref enter, ref leave);
	}
	private static bool ClipAxis(float origin, float movement, float radius, ref float enter, ref float leave)
	{
		if (Mathf.IsZeroApprox(movement)) return Mathf.Abs(origin) <= radius;
		float first = (-radius - origin) / movement, last = (radius - origin) / movement;
		enter = Mathf.Max(enter, Mathf.Min(first, last));
		leave = Mathf.Min(leave, Mathf.Max(first, last));
		return enter <= leave;
	}
}
