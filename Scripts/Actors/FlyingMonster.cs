using Godot;

namespace Zaomeng;

/// <summary>独立飞行运动模式，沿目标胸口追踪；不把飞行怪强行落到地上。</summary>
public partial class FlyingMonster : Monster
{
	[Export] public float FlightHeight { get; set; } = 35;
	public override void _PhysicsProcess(double delta)
	{
		Gravity = 0;
		if (AiEnabled && !IsDead && State == ActorState.Free
			&& GetNodeOrNull<CharacterActor>(TargetPath) is { IsDead: false } target)
		{
			float distance = target.GlobalPosition.Y - FlightHeight - GlobalPosition.Y;
			Velocity = new(Velocity.X, Mathf.Clamp(distance * 3, -MoveSpeed, MoveSpeed));
		}
		else if (!IsDead) Velocity = new(Velocity.X, 0);
		else Gravity = 1000;
		base._PhysicsProcess(delta);
	}
}
