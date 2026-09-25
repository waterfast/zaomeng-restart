using Godot;

namespace Zaomeng;

/// <summary>一次技能释放的运行时计时；配置数据留在 SkillDefinition 中。</summary>
public sealed class SkillCast
{
    public SkillDefinition Definition { get; }
    public bool IsHitActive { get; private set; }

    private readonly AnimationPlayer _animator;
    private readonly HitBox _hitBox;
    private readonly Node2D _facing;
    private readonly Node _worldParent;
    private bool _effectReleased;

    public SkillCast(SkillDefinition definition, AnimationPlayer animator, HitBox hitBox,
        Node2D facing, Node worldParent)
    {
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
        if (!_effectReleased && Definition.EffectScene is { } effectScene
            && frame >= Definition.EffectFrame)
        {
            _effectReleased = true;
            if (Definition.EffectFollowsActor)
                ActorEffectSpawner.SpawnAttached(effectScene, _facing,
                    Definition.EffectOffset, Definition.EffectLifetime);
            else
                ActorEffectSpawner.SpawnInWorld(effectScene, _worldParent,
                    _facing.ToGlobal(Definition.EffectOffset), Definition.EffectLifetime);
        }

		IsHitActive = Definition.Hit != null && Definition.HitStartFrame >= 0
			&& Definition.HitEndFrame > Definition.HitStartFrame
			&& frame >= Definition.HitStartFrame
			&& frame < Definition.HitEndFrame;
        _hitBox.Active = IsHitActive;
    }

    public void Stop()
    {
        IsHitActive = false;
        _hitBox.Active = false;
    }
}
