using Godot;

namespace Zaomeng;

/// <summary>记录一次动作的起招速度与面向，用动画时间决定本帧水平速度。</summary>
public sealed class ActionMotionPlayback
{
	private readonly ActionMotion _motion;
	private readonly AnimationPlayer _animator;
	private readonly StringName _animation;
	private readonly int _framesPerSecond;
	private readonly float _entryVelocityX;
	private readonly int _facingDirection;

	public ActionMotionPlayback(ActionMotion motion, AnimationPlayer animator, StringName animation,
		int framesPerSecond, float entryVelocityX, int facingDirection)
	{
		_motion = motion;
		_animator = animator;
		_animation = animation;
		_framesPerSecond = framesPerSecond;
		_entryVelocityX = entryVelocityX;
		_facingDirection = facingDirection;
	}

	public float GetHorizontalSpeed()
	{
		if (!_animator.IsPlaying() || _animator.CurrentAnimation != _animation) return 0;

		double animationTime = _animator.CurrentAnimationPosition;
		double frame = animationTime * _framesPerSecond;
		if (frame < _motion.StartFrame || (_motion.EndFrame >= 0 && frame >= _motion.EndFrame))
			return 0;

		return _motion.Mode switch
		{
			ActionMotionMode.CarryMomentum => Mathf.MoveToward(_entryVelocityX, 0,
				_motion.MomentumDeceleration * (float)(animationTime - _motion.StartFrame / (double)_framesPerSecond)),
			ActionMotionMode.FacingDash => _facingDirection * _motion.Speed,
			_ => 0
		};
	}
}
