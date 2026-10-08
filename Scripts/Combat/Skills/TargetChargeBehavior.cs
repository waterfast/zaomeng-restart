using System.Linq;
using Godot;

namespace Zaomeng.Skills;

/// <summary>起招锁定敌人位置；俯冲用真实身体碰撞移动，不穿越地图矩形。</summary>
public partial class TargetChargeBehavior : SkillBehavior
{
	[Export] public float StartSeconds { get; set; } = 0.1f;
	[Export] public float EndSeconds { get; set; } = 0.8f;
	[Export] public float Speed { get; set; } = 1200;
	private Vector2 _destination;
	public override void ValidateConfiguration() => Require(float.IsFinite(StartSeconds) && float.IsFinite(EndSeconds)
		&& StartSeconds >= 0 && EndSeconds > StartSeconds && float.IsFinite(Speed) && Speed > 0, "冲撞时机或速度无效。");
	protected override void OnBegin()
	{
		var target = Actor.GetTree().GetNodesInGroup("combat_actors").OfType<CharacterActor>()
			.Where(a => a.Team != Actor.Team && !a.IsDead && a.Visible)
			.OrderBy(a => a.GlobalPosition.DistanceSquaredTo(Actor.GlobalPosition)).FirstOrDefault();
		_destination = target?.GlobalPosition ?? Actor.GlobalPosition;
	}
	protected override void OnTick(float delta)
	{
		if (Elapsed < StartSeconds || Elapsed > EndSeconds || delta <= 0) return;
		Vector2 next = Actor.GlobalPosition.MoveToward(_destination, Speed * delta);
		Actor.MoveAndCollide(next - Actor.GlobalPosition);
	}
}
