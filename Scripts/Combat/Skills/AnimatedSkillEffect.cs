using System;
using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng.Skills;

/// <summary>播放从旧场景核对提取的纯美术轨道，独立拥有命中和清理状态。</summary>
public partial class AnimatedSkillEffect : Node2D
{
	[Export] public StringName Animation { get; set; } = "effect";
	[Export] public float Lifetime { get; set; } = 1;
	[Export] public float MoveSpeed { get; set; }
	[Export] public HitDefinition? Hit { get; set; }
	[Export] public float[] HitTimes { get; set; } = [];
	[Export] public float Radius { get; set; } = 100;
	[Export] public float VerticalRadius { get; set; } = 100;
	[Export] public float HealRatio { get; set; }
	[Export] public float HealInterval { get; set; } = 1;
	[Export] public PackedScene? SatelliteScene { get; set; }
	[Export] public float SatelliteInterval { get; set; } = 5f / 60;
	[Export] public float SatelliteAngleStep { get; set; } = 30;
	private CharacterActor _source = null!;
	private SkillDefinition _skill = null!;
	private AttackParameters _parameters = AttackParameters.Default;
	private SkillEffectCue _cue = null!;
	private int _revision;
	private int _level;
	private int _direction;
	private float _time;
	private int _nextHit;
	private int _nextHeal = 1;
	private int _satellites;
	private AnimationPlayer _animator = null!;
	public void ValidateConfiguration()
	{
		var animator = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animator is null || !animator.HasAnimation(Animation) || !float.IsFinite(Lifetime) || Lifetime <= 0
			|| !float.IsFinite(MoveSpeed) || !float.IsFinite(Radius) || Radius <= 0
			|| !float.IsFinite(VerticalRadius) || VerticalRadius <= 0 || !float.IsFinite(HealRatio) || HealRatio < 0 || HealRatio > 1
			|| !float.IsFinite(HealInterval) || HealInterval <= 0 || (HitTimes.Length > 0 && Hit is null)
			|| !float.IsFinite(SatelliteInterval) || SatelliteInterval <= 0 || !float.IsFinite(SatelliteAngleStep))
			throw new InvalidOperationException("动画特效缺少有效动画、寿命或命中、治疗配置。");
		float previous = -1;
		foreach (float time in HitTimes)
		{
			if (!float.IsFinite(time) || time < 0 || time <= previous || time >= Lifetime)
				throw new InvalidOperationException("特效命中时机必须在寿命内严格递增。");
			previous = time;
		}
		if (SatelliteScene is { } scene)
		{
			var satellite = scene.Instantiate<SkillProjectile>();
			try { satellite.ValidateConfiguration(); }
			finally { satellite.Free(); }
		}
	}
	public void Configure(CharacterActor source, SkillDefinition skill, int level, AttackParameters parameters, SkillEffectCue cue)
	{
		_source = source;
		_skill = skill;
		_level = level;
		_parameters = parameters;
		_cue = cue;
		_revision = source.SpawnRevision;
		_direction = source.FacingDirection;
	}
	public override void _Ready()
	{
		if (_source is null) { QueueFree(); return; }
		Scale = _cue.Scale * _parameters.VisualScale;
		if (_cue.Mirror) Scale = new(-_direction * Scale.X, Scale.Y);
		GlobalPosition = _source.GlobalPosition + new Vector2(_cue.Offset.X * _direction, _cue.Offset.Y);
		_animator = GetNode<AnimationPlayer>("AnimationPlayer");
		_animator.SpeedScale = _parameters.EffectSpeed;
		_animator.Play(Animation);
		_animator.Advance(0);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_source) || _source.IsDead || !_source.Visible || _source.SpawnRevision != _revision)
		{ QueueFree(); return; }
		_time += (float)delta * _parameters.EffectSpeed;
		if (_cue.FollowActor)
			GlobalPosition = _source.GlobalPosition + new Vector2(_cue.Offset.X * _direction, _cue.Offset.Y);
		else Position += new Vector2(MoveSpeed * _direction * _parameters.ProjectileSpeed * (float)delta, 0);
		while (SatelliteScene is { } scene && _time >= _satellites * SatelliteInterval && _satellites * SatelliteInterval < Lifetime)
		{
			var projectile = scene.Instantiate<SkillProjectile>();
			projectile.Configure(_source, _level, _skill, _parameters);
			projectile.TravelAngleDegrees = (_satellites++ % 12 + 1) * SatelliteAngleStep;
			_source.GetParent().AddChild(projectile);
			projectile.GlobalPosition = GlobalPosition;
		}
		while (_nextHit < HitTimes.Length && _time >= HitTimes[_nextHit])
		{
			_nextHit++;
			foreach (var node in GetTree().GetNodesInGroup("combat_actors"))
				if (node is CharacterActor { Visible: true, IsDead: false } target && target.Team != _source.Team
					&& Mathf.Abs(target.GlobalPosition.X - GlobalPosition.X) <= Radius * _cue.Scale.X * _parameters.Range
					&& Mathf.Abs(target.GlobalPosition.Y - GlobalPosition.Y) <= VerticalRadius * _parameters.Range)
					CombatResolver.Resolve(_source, target, Hit!, _level, _skill);
		}
		while (HealRatio > 0 && _time >= _nextHeal * HealInterval && _nextHeal * HealInterval < Lifetime)
		{
			_nextHeal++;
			_source.Heal(_source.MaxHealth * HealRatio);
		}
		if (_time >= Lifetime * _parameters.EffectLifetime) QueueFree();
	}
}
