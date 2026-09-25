using Godot;

namespace Zaomeng;

public sealed class CharacterMotor
{
	public float ExternalVelocityX { get; private set; }

	public void ApplyKnockback(CharacterBody2D body, Vector2 impulse)
	{
		ExternalVelocityX = impulse.X;
		if (!Mathf.IsZeroApprox(impulse.Y)) body.Velocity = new(body.Velocity.X, impulse.Y);
	}

	public void Step(CharacterBody2D body, float moveSpeed, float gravity, float friction, float delta)
	{
		// Input cannot erase knockback on the following frame.
		float vertical = body.IsOnFloor() && body.Velocity.Y >= 0 ? 0 : body.Velocity.Y + gravity * delta;
		body.Velocity = new(moveSpeed + ExternalVelocityX, vertical);
		body.MoveAndSlide();
		ExternalVelocityX = Mathf.MoveToward(ExternalVelocityX, 0, friction * delta);
	}
}
