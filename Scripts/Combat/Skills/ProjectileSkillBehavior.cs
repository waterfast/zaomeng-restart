using Godot;

namespace Zaomeng.Skills;

public partial class ProjectileSkillBehavior : SkillBehavior
{
	[Export] public float Delay { get; set; } = 0.5f;
	[Export] public PackedScene ProjectileScene { get; set; } = null!;
	[Export] public Vector2 SpawnOffset { get; set; } = new(55, -45);
	private bool _released;
	public override string DescribeValues(int level)
	{
		var projectile = ProjectileScene.Instantiate<SkillProjectile>();
		try { return $"弹体倍率 {projectile.Hit.MinimumMultiplier(level):0.##}"; }
		finally { projectile.Free(); }
	}
	public override void ValidateConfiguration()
	{
		Require(float.IsFinite(Delay) && Delay >= 0 && SpawnOffset.IsFinite() && ProjectileScene is not null, "弹体技能缺少场景或释放时机无效。");
		Node projectile = ProjectileScene!.Instantiate();
		try
		{
			Require(projectile is SkillProjectile { Hit: not null, Frames: not null } configured && configured.Frames.HasAnimation(configured.Animation) &&
				float.IsFinite(configured.Speed) && configured.Speed > 0 && float.IsFinite(configured.Lifetime) && configured.Lifetime > 0 &&
				float.IsFinite(configured.HitRadius) && configured.HitRadius > 0 && float.IsFinite(configured.VisualScale) && configured.VisualScale > 0,
				"弹体场景缺少脚本、命中或动画配置。");
		}
		finally { projectile.Free(); }
	}
	protected override void OnTick(float delta)
	{
		if (_released || Elapsed < Delay) return;
		_released = true;
		var projectile = ProjectileScene.Instantiate<SkillProjectile>();
		projectile.Configure(Actor, Level, Definition);
		Actor.GetParent().AddChild(projectile);
		projectile.GlobalPosition = Actor.GlobalPosition + new Vector2(SpawnOffset.X * Actor.FacingDirection, SpawnOffset.Y);
	}
}
