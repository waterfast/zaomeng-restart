using Godot;

namespace Zaomeng.Skills;

public partial class FireSlashBehavior : SkillBehavior
{
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public HitDefinition AirHit { get; set; } = null!;
	[Export] public HitDefinition DiveHit { get; set; } = null!;
	[Export] public HitDefinition LandingHit { get; set; } = null!;
	[Export] public float Gravity { get; set; } = 850;
	[Export] public float RiseSpeed { get; set; } = 430;
	[Export] public float DiveSpeed { get; set; } = 900;
	[Export] public float FirstHitDelay { get; set; } = 0.12f;
	[Export] public float AirHitInterval { get; set; } = 0.18f;
	[Export] public float DiveDelay { get; set; } = 0.5f;
	[Export] public float AirRadius { get; set; } = 140;
	[Export] public float LandingRadius { get; set; } = 230;
	private float _normalGravity;
	private int _phase;
	private int _airHits;
	public override string DescribeValues(int level) =>
		$"空中倍率 {AirHit.MinimumMultiplier(level):0.##}；下冲倍率 {DiveHit.MinimumMultiplier(level):0.##}；落地倍率 {LandingHit.MinimumMultiplier(level):0.##}";
	public override void ValidateConfiguration()
	{
		Require(Frames is not null && AirHit is not null && DiveHit is not null && LandingHit is not null, "火魔斩缺少帧图或命中资源。");
		foreach (string animation in new[] { "hmz_pro", "hmz_1", "hmz_flush", "hmz_2" })
			Require(Frames!.HasAnimation(animation), $"火魔斩缺少动画 {animation}。");
		foreach (float value in new[] { Gravity, RiseSpeed, DiveSpeed, FirstHitDelay, AirHitInterval, DiveDelay, AirRadius, LandingRadius })
			Require(float.IsFinite(value) && value > 0, "火魔斩的速度、半径或时间无效。");
	}
	protected override void OnBegin()
	{
		_normalGravity = Actor.Gravity;
		Actor.Gravity = Gravity;
		Actor.Velocity = new(0, -RiseSpeed);
		ShowSlash("hmz_pro", new(0, -75), 0.4f);
	}
	protected override void OnTick(float delta)
	{
		while (_airHits < 2 && Elapsed >= FirstHitDelay + _airHits * AirHitInterval)
		{
			_airHits++;
			ShowSlash("hmz_1", new(0, -35), 0.3f);
			HitNearby(AirRadius, AirHit);
		}
		if (_phase == 0 && Elapsed >= DiveDelay)
		{
			_phase = 1;
			Actor.Velocity = new(0, DiveSpeed);
			ShowSlash("hmz_flush", new(0, -25), 0.5f);
			HitNearby(AirRadius, DiveHit);
		}
		if (_phase == 1 && Actor.IsOnFloor())
		{
			_phase = 2;
			Actor.StartPendingSkillCooldown(Definition);
			ShowSlash("hmz_2", new(0, -45), 0.8f);
			HitNearby(LandingRadius, LandingHit);
		}
	}
	private void HitNearby(float radius, HitDefinition hit)
	{
		foreach (Node node in Actor.GetTree().GetNodesInGroup("monsters"))
			if (node is Monster { Visible: true } monster && !monster.IsDead &&
				Mathf.Abs(monster.GlobalPosition.X - Actor.GlobalPosition.X) < radius &&
				Mathf.Abs(monster.GlobalPosition.Y - Actor.GlobalPosition.Y) < 140)
				CombatResolver.Resolve(Actor, monster, hit, Level);
	}
	private void ShowSlash(string animation, Vector2 offset, float lifetime)
	{
		var sprite = new AnimatedSprite2D { SpriteFrames = Frames, Position = Actor.GlobalPosition + offset, Scale = new(-Actor.FacingDirection, 1) };
		Actor.GetParent().AddChild(sprite);
		sprite.Play(animation);
		var timer = new Timer { WaitTime = lifetime, OneShot = true };
		sprite.AddChild(timer);
		timer.Timeout += sprite.QueueFree;
		timer.Start();
	}
	protected override void OnStop()
	{
		Actor.Gravity = _normalGravity;
		// 中断未落地的动作也不能绕过已经快照的冷却。
		Actor.StartPendingSkillCooldown(Definition);
	}
}
