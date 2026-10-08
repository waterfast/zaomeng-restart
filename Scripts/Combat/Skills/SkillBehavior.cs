using System;
using Godot;

namespace Zaomeng.Skills;

/// <summary>每次施放独立实例。终止原因统一处理，子类只实现行为与临时状态恢复。</summary>
public abstract partial class SkillBehavior : Node
{
	protected CharacterActor Actor { get; private set; } = null!;
	protected SkillDefinition Definition { get; private set; } = null!;
	protected float Elapsed { get; private set; }
	protected int Level { get; private set; } = 1;
	protected Zaomeng.Combat.Effects.AttackParameters Parameters { get; private set; } = Zaomeng.Combat.Effects.AttackParameters.Default;
	private bool _stopped;
	public abstract void ValidateConfiguration();
	public virtual string DescribeValues(int level) => "";
	protected static void Require(bool valid, string message)
	{
		if (!valid) throw new InvalidOperationException(message);
	}
	public void Begin(CharacterActor actor, SkillDefinition definition)
	{
		Actor = actor;
		Definition = definition;
		Level = actor.CurrentHitLevel;
		Parameters = actor.CurrentAttackParameters;
		actor.Animator.AnimationFinished += OnAnimationFinished;
		OnBegin();
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(Actor) || Actor.IsDead || Actor.State != ActorState.Attacking || Actor.Animator.CurrentAnimation != Definition.Animation)
		{ Stop(); return; }
		// 与命中窗口使用同一动画时钟，渲染卡顿不会让释放时间落后于动作。
		Elapsed = (float)Actor.Animator.CurrentAnimationPosition;
		OnTick((float)delta);
	}
	public void Complete(StringName animation) => OnAnimationFinished(animation);
	private void OnAnimationFinished(StringName animation)
	{
		if (_stopped || animation != Definition.Animation) return;
		Elapsed = (float)Actor.Animator.GetAnimation(animation).Length;
		OnTick(0);
		Stop();
	}
	public void Stop()
	{
		if (_stopped) return;
		_stopped = true;
		if (IsInstanceValid(Actor))
		{
			Actor.Animator.AnimationFinished -= OnAnimationFinished;
			OnStop();
		}
		SetPhysicsProcess(false);
		QueueFree();
	}
	public override void _ExitTree()
	{
		if (!_stopped && IsInstanceValid(Actor))
		{
			Actor.Animator.AnimationFinished -= OnAnimationFinished;
			OnStop();
		}
		_stopped = true;
	}
	protected virtual void OnBegin() { }
	protected virtual void OnStop() { }
	protected abstract void OnTick(float delta);
}
