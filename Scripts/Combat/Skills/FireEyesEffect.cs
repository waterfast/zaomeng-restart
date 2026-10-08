using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng.Skills;

public partial class FireEyesEffect : Node2D
{
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public HitDefinition Hit { get; set; } = null!;
	[Export] public int HitCount { get; set; } = 3;
	[Export] public float Interval { get; set; } = 1.1f;
	[Export] public float[] HitTimes { get; set; } = [0.1f, 0.3f, 0.5f];
	[Export] public AudioStream? ExplosionSound { get; set; }
	private CharacterActor _player = null!;
	private CharacterActor _target = null!;
	private AnimatedSprite2D _sprite = null!;
	private float _time;
	private int _hits;
	private int _pulses;
	private int _level;
	private int _sourceRevision;
	private int _targetRevision;
	private SkillDefinition? _sourceSkill;
	private AttackParameters _parameters = AttackParameters.Default;
	public void Configure(CharacterActor player, CharacterActor target, int? level = null, SkillDefinition? sourceSkill = null, AttackParameters? parameters = null)
	{
		_parameters = parameters ?? player.CurrentAttackParameters;
		_player = player; _target = target; _level = level ?? player.CurrentHitLevel;
		_sourceRevision = player.SpawnRevision;
		_targetRevision = target.SpawnRevision;
		_sourceSkill = sourceSkill ?? player.CurrentSkill;
	}
	public override void _Ready()
	{
		_sprite = new AnimatedSprite2D { SpriteFrames = Frames, Scale = Vector2.One * _parameters.VisualScale,
			SpeedScale = _parameters.EffectSpeed, ZIndex = 6 };
		AddChild(_sprite);
		GlobalPosition = _target.GlobalPosition;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_player) || !IsInstanceValid(_target) || _player.IsDead || !_player.Visible || _target.IsDead || !_target.Visible
			|| _player.SpawnRevision != _sourceRevision || _target.SpawnRevision != _targetRevision)
		{ QueueFree(); return; }
		GlobalPosition = _target.GlobalPosition;
		_time += (float)delta;
		float clock = _time * _parameters.EffectSpeed;
		if (_hits < HitCount && clock >= _hits * Interval)
		{
			_hits++;
			_sprite.Play("hyjj");
			_sprite.SetFrameAndProgress(0, 0);
			Zaomeng.Audio.AudioManager.Instance?.PlayEffect(ExplosionSound);
		}
		// 一轮爆炸包含三个独立命中点；间歇留白来自旧动画末尾的空帧。
		while (_pulses < HitCount * HitTimes.Length &&
			clock >= (_pulses / HitTimes.Length) * Interval + HitTimes[_pulses % HitTimes.Length])
		{
			_pulses++;
			CombatResolver.Resolve(_player, _target, Hit, _level, _sourceSkill);
		}
		if (_time >= HitCount * Interval / _parameters.EffectSpeed * _parameters.EffectLifetime) QueueFree();
	}
}
