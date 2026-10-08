using Godot;

namespace Zaomeng.Skills;

public partial class ProjectileSkillBehavior : SkillBehavior
{
	[Export] public AudioStream? ReleaseSound { get; set; }
	[Export] public float[] ReleaseTimes { get; set; } = [0.5f];
	[Export] public PackedScene ProjectileScene { get; set; } = null!;
	[Export] public Vector2 SpawnOffset { get; set; } = new(55, -45);
	[Export] public NodePath OriginPath { get; set; } = new("");
	[Export] public float[] SpreadDegrees { get; set; } = [0];
	private Node2D? _origin;
	private int _releasedCount;
	public override string DescribeValues(int level)
	{
		var projectile = ProjectileScene.Instantiate<SkillProjectile>();
		try { return $"弹体倍率 {projectile.Hit.MinimumMultiplier(level):0.##}"; }
		finally { projectile.Free(); }
	}
	public override void ValidateConfiguration()
	{
		Require(ReleaseTimes.Length > 0 && SpawnOffset.IsFinite() && ProjectileScene is not null, "弹体技能缺少场景或释放时机。");
		Require(SpreadDegrees.Length > 0 && System.Array.TrueForAll(SpreadDegrees, float.IsFinite), "弹体角度无效。");
		float previousTime = -1;
		foreach (float time in ReleaseTimes)
		{
			Require(float.IsFinite(time) && time >= 0 && time > previousTime, "弹体释放时机必须非负且严格递增。");
			previousTime = time;
		}
		Node projectile = ProjectileScene!.Instantiate();
		try
		{
			Require(projectile is SkillProjectile, "弹体场景缺少对应脚本。");
			((SkillProjectile)projectile).ValidateConfiguration();
		}
		finally { projectile.Free(); }
	}
	protected override void OnBegin()
	{
		if (!OriginPath.IsEmpty)
			_origin = Actor.GetNode<Node2D>(OriginPath);
	}
	protected override void OnTick(float delta)
	{
		// 卡顿跨过多个释放点时逐个补发，数量不受物理帧率影响。
		while (_releasedCount < ReleaseTimes.Length && Elapsed >= ReleaseTimes[_releasedCount])
		{
			_releasedCount++;
			Zaomeng.Audio.AudioManager.Instance?.PlayEffect(ReleaseSound);
			foreach (float angle in SpreadDegrees)
			{
				var projectile = (Parameters.ProjectileScene ?? ProjectileScene).Instantiate<SkillProjectile>();
				projectile.Configure(Actor, Level, Definition, Parameters);
				projectile.TravelAngleDegrees = angle;
				Actor.GetParent().AddChild(projectile);
				projectile.GlobalPosition = _origin?.GlobalPosition ??
					Actor.GlobalPosition + new Vector2(SpawnOffset.X * Actor.FacingDirection, SpawnOffset.Y);
			}
		}
	}
}
