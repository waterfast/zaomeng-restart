using Godot;

namespace Zaomeng.Skills;

public partial class BurstSkillBehavior : SkillBehavior
{
	[Export] public BurstAttackDefinition Burst { get; set; } = null!;
	[Export] public bool TargetEnemy { get; set; }
	private int _released;
	private Vector2 _origin;
	public override void ValidateConfiguration() { Require(Burst is not null, "技能缺少范围爆发配置。"); Burst!.Validate(); }
	public override string DescribeValues(int level) => $"{Burst.Positions.Length}道范围效果，每段倍率 {Burst.Hits[0].Hit!.MinimumMultiplier(level):0.###}";
	protected override void OnBegin()
	{
		_origin = Actor.GlobalPosition;
		if (!TargetEnemy) return;
		float nearest = float.MaxValue;
		foreach (var node in Actor.GetTree().GetNodesInGroup("combat_actors"))
			if (node is CharacterActor { IsDead: false, Visible: true } target && target.Team != Actor.Team)
			{
				float distance = Actor.GlobalPosition.DistanceSquaredTo(target.GlobalPosition);
				if (distance >= nearest) continue;
				nearest = distance;
				_origin = target.GlobalPosition;
			}
	}
	protected override void OnTick(float delta)
	{
		while (_released < Burst.ReleaseTimes.Length && Elapsed >= Burst.ReleaseTimes[_released])
			Burst.Release(Actor, Definition, Level, _released++, TargetEnemy ? _origin : Actor.GlobalPosition, Actor.FacingDirection, Parameters);
	}
}
