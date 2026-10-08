using System.Collections.Generic;
using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng.Skills;

/// <summary>释放后独立播放的有限时长范围爆发，命中时钟与视觉帧同步。</summary>
public partial class AreaAttackEffect : Node2D
{
	public bool AlignVisualToGround { get; set; }
	public float VerticalRadius { get; set; } = 140;
	public Material? VisualMaterial { get; set; }
	private CharacterActor _source = null!;
	private SkillDefinition _skill = null!;
	private int _sourceRevision;
	private int _level;
	private float _radius;
	private Godot.Collections.Array<HitEvent> _windows = new();
	private AnimatedSprite2D _sprite = null!;
	private readonly HashSet<ulong> _hitTargets = new();
	private int _activeWindow = -1;
	private AttackParameters _parameters = AttackParameters.Default;
	private float _elapsed;
	private float _duration;

	public void Configure(CharacterActor source, SkillDefinition skill, int level, float radius,
		Godot.Collections.Array<HitEvent> windows, SpriteFrames frames, StringName animation,
		Vector2 scale, Vector2 offset, AttackParameters parameters)
	{
		_parameters = parameters;
		_source = source;
		_sourceRevision = source.SpawnRevision;
		_skill = skill;
		_level = level;
		_radius = radius * parameters.Range;
		_windows = windows;
		_sprite = new AnimatedSprite2D { SpriteFrames = frames, Animation = animation, Scale = scale * parameters.VisualScale, Offset = offset,
			Material = VisualMaterial, ZIndex = 6, SpeedScale = parameters.EffectSpeed };
		float framesDuration = 0;
		for (int i = 0; i < frames.GetFrameCount(animation); i++) framesDuration += (float)frames.GetFrameDuration(animation, i);
		_duration = framesDuration / (float)frames.GetAnimationSpeed(animation) / parameters.EffectSpeed * parameters.EffectLifetime;
		AddChild(_sprite);
		if (AlignVisualToGround)
		{
			_sprite.FrameChanged += AlignVisual;
			AlignVisual();
		}
	}
	private void AlignVisual()
	{
		// 裁去透明画布后各帧尺寸不同，以脚点固定底边，避免特效陷入地面。
		Texture2D? texture = _sprite.SpriteFrames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
		if (texture is not null) _sprite.Position = new(0, -texture.GetHeight() * _sprite.Scale.Y / 2);
	}

	public override void _Ready()
	{
		_sprite.Play();
	}

	public override void _PhysicsProcess(double delta)
	{
		_elapsed += (float)delta;
		if (_elapsed >= _duration) { QueueFree(); return; }
		if (!IsInstanceValid(_source) || _source.IsDead || !_source.Visible || _source.SpawnRevision != _sourceRevision)
		{
			QueueFree();
			return;
		}
		int activeWindow = -1;
		for (int i = 0; i < _windows.Count; i++)
			if (_sprite.Frame >= _windows[i].StartFrame && _sprite.Frame < _windows[i].EndFrame)
			{
				activeWindow = i;
				break;
			}
		if (_activeWindow != activeWindow)
		{
			_activeWindow = activeWindow;
			_hitTargets.Clear();
		}
		if (activeWindow < 0) return;
		foreach (Node node in GetTree().GetNodesInGroup("combat_actors"))
		{
			if (node is not CharacterActor { Visible: true, IsDead: false } target || target.Team == _source.Team
				|| _hitTargets.Contains(target.GetInstanceId()) || Mathf.Abs(target.GlobalPosition.X - GlobalPosition.X) > _radius
				|| Mathf.Abs(target.GlobalPosition.Y - GlobalPosition.Y) > VerticalRadius * _parameters.Range) continue;
			if (CombatResolver.Resolve(_source, target, _windows[activeWindow].Hit!, _level, _skill))
				_hitTargets.Add(target.GetInstanceId());
		}
	}
}
