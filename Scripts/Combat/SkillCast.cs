using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng;

/// <summary>一次技能释放的运行时计时；配置数据留在 SkillDefinition 中。</summary>
public sealed class SkillCast
{
	public SkillDefinition Definition { get; }
	public HitDefinition? ActiveHit { get; private set; }

	private readonly AnimationPlayer _animator;
	private readonly HitBox _hitBox;
	private readonly Node2D _facing;
	private readonly Node _worldParent;
	private bool _effectReleased;
	private int _activeWindow = -1;
	private readonly AttackParameters _parameters;

	public SkillCast(SkillDefinition definition, AnimationPlayer animator, HitBox hitBox,
		Node2D facing, Node worldParent, AttackParameters parameters)
	{
		_parameters = parameters;
		Definition = definition;
		_animator = animator;
		_hitBox = hitBox;
		_facing = facing;
		_worldParent = worldParent;
	}

	public void Update()
	{
		if (_animator.CurrentAnimation != Definition.Animation)
		{
			Stop();
			return;
		}

		// AnimationPlayer 的时间单位是秒；配置使用动画的帧号。
		double frame = _animator.CurrentAnimationPosition * Definition.FramesPerSecond;
		if (!_effectReleased && (_parameters.EffectScene ?? Definition.EffectScene) is { } effectScene
			&& frame >= Definition.EffectFrame)
		{
			_effectReleased = true;
			if (Definition.EffectFollowsActor)
				ActorEffectSpawner.SpawnAttached(effectScene, _facing,
					Definition.EffectOffset, Definition.EffectLifetime, _parameters);
			else
				ActorEffectSpawner.SpawnInWorld(effectScene, _worldParent,
					_facing.ToGlobal(Definition.EffectOffset), Definition.EffectLifetime, _parameters);
		}

		int window = -1;
		for (int i = 0; i < Definition.Hits.Count; i++)
		{
			HitEvent? hitEvent = Definition.Hits[i];
			if (hitEvent?.Hit != null && frame >= hitEvent.StartFrame && frame < hitEvent.EndFrame)
			{
				window = i;
				break;
			}
		}

		if (window != _activeWindow)
		{
			// 同一技能的下一波允许再次命中已受击的目标。
			_hitBox.BeginHitWindow();
			_activeWindow = window;
		}
		ActiveHit = window >= 0 ? Definition.Hits[window].Hit : null;
		_hitBox.Active = ActiveHit != null;
	}

	public void Stop()
	{
		ActiveHit = null;
		_activeWindow = -1;
		_hitBox.Active = false;
	}
}
