using Godot;

namespace Zaomeng.Skills;

public partial class FireEyesBehavior : SkillBehavior
{
	[Export] public float Delay { get; set; } = 0.7f;
	[Export] public float Range { get; set; } = 2000;
	[Export] public PackedScene ImpactScene { get; set; } = null!;
	private bool _released;
	public override string DescribeValues(int level)
	{
		var fire = ImpactScene.Instantiate<FireEyesEffect>();
		try { return $"每跳倍率 {fire.Hit.MinimumMultiplier(level):0.##}"; }
		finally { fire.Free(); }
	}
	public override void ValidateConfiguration()
	{
		Require(float.IsFinite(Delay) && Delay >= 0 && float.IsFinite(Range) && Range > 0 && ImpactScene is not null, "火眼行为的时机、距离或效果场景无效。");
		Node effect = ImpactScene!.Instantiate();
		try
		{
			Require(effect is FireEyesEffect { Hit: not null, Frames: not null } fire && fire.Frames.HasAnimation("hyjj") &&
				fire.HitCount > 0 && float.IsFinite(fire.Interval) && fire.Interval > 0 && fire.HitTimes.Length > 0 &&
				System.Array.TrueForAll(fire.HitTimes, time => float.IsFinite(time) && time >= 0 && time < fire.Interval), "火眼效果缺少命中或动画配置。");
		}
		finally { effect.Free(); }
	}
	protected override void OnTick(float delta)
	{
		if (_released || Elapsed < Delay) return;
		_released = true;
		CharacterActor? target = null;
		float distance = Range * Parameters.Range;
		foreach (Node node in Actor.GetTree().GetNodesInGroup("combat_actors"))
		{
			if (node is not CharacterActor monster || monster.Team == Actor.Team || monster.IsDead || !monster.Visible) continue;
			float offset = (monster.GlobalPosition.X - Actor.GlobalPosition.X) * Actor.FacingDirection;
			if (offset < 0 || offset >= distance) continue;
			distance = offset;
			target = monster;
		}
		if (target is null) return;
		var fire = ImpactScene.Instantiate<FireEyesEffect>();
		fire.Configure(Actor, target, Level, Definition, Parameters);
		Actor.GetParent().AddChild(fire);
	}
}
