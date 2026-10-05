using Godot;

namespace Zaomeng.Skills;

public partial class CloudFlightBehavior : SkillBehavior
{
	[Export] public float LiftSpeed { get; set; } = 430;
	[Export] public Key RiseKey { get; set; } = Key.W;
	private float _gravity;
	public override void ValidateConfiguration() => Require(float.IsFinite(LiftSpeed) && LiftSpeed > 0 && RiseKey is >= Key.A and <= Key.Z, "飞行行为的升力或按键无效。");
	protected override void OnBegin() => _gravity = Actor.Gravity;
	protected override void OnTick(float delta)
	{
		Actor.Gravity = 0;
		Actor.Velocity = new(Actor.Velocity.X, Input.IsPhysicalKeyPressed(RiseKey) ? -LiftSpeed : 0);
	}
	protected override void OnStop() => Actor.Gravity = _gravity;
}
