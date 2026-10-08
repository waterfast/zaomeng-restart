using Godot;

namespace Zaomeng.Skills;

public partial class FireSlashBehavior : SkillBehavior
{
	[Export] public AudioStream? AirSound { get; set; }
	[Export] public AudioStream? LandingSound { get; set; }
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
		Actor.Velocity = new(0, -RiseSpeed * Parameters.MotionSpeed);
		ShowSlash("hmz_pro", new(25 * Actor.FacingDirection, -80), 0.6f, new(1.15f, 1.15f), reverse: true,
			tint: new(1, 0.917647f, 1));
	}
	protected override void OnTick(float delta)
	{
		while (_airHits < 2 && Elapsed >= FirstHitDelay + _airHits * AirHitInterval)
		{
			_airHits++;
			if (_airHits == 1)
			{
				Zaomeng.Audio.AudioManager.Instance?.PlayEffect(AirSound);
				// 旧空中火圈共25帧，9帧/秒再乘1.4；不能按一次命中的间隔截断动画。
				ShowSlash("hmz_1", new(-25 * Actor.FacingDirection, 45), 25f / (9 * 1.4f),
					new(1.17f, 1.17f), playbackSpeed: 1.4f);
			}
			HitNearby(AirRadius, AirHit);
		}
		if (_phase == 0 && Elapsed >= DiveDelay)
		{
			_phase = 1;
			Actor.Velocity = new(0, DiveSpeed * Parameters.MotionSpeed);
			ShowSlash("hmz_flush", new(65 * Actor.FacingDirection, 90), 1f / (15 * 0.7f), new(1.1f, 1.1f),
				reverse: true, playbackSpeed: 0.7f, tint: new(1, 0.92549f, 1));
			HitNearby(AirRadius, DiveHit);
		}
		if (_phase == 1 && Actor.IsOnFloor())
		{
			_phase = 2;
			Zaomeng.Audio.AudioManager.Instance?.PlayEffect(LandingSound);
			Actor.StartPendingSkillCooldown(Definition);
			ShowSlash("hmz_2", Vector2.Zero, 0.6f, new(1.5f, 2.2f), tint: new(1, 0.984314f, 1));
			HitNearby(LandingRadius, LandingHit);
		}
	}
	private void HitNearby(float radius, HitDefinition hit)
	{
		foreach (Node node in Actor.GetTree().GetNodesInGroup("combat_actors"))
			if (node is CharacterActor { Visible: true } monster && monster.Team != Actor.Team && !monster.IsDead &&
				Mathf.Abs(monster.GlobalPosition.X - Actor.GlobalPosition.X) < radius * Parameters.Range &&
				Mathf.Abs(monster.GlobalPosition.Y - Actor.GlobalPosition.Y) < 140 * Parameters.Range)
				CombatResolver.Resolve(Actor, monster, hit, Level, Definition);
	}
	private void ShowSlash(string animation, Vector2 offset, float lifetime, Vector2 scale, bool reverse = false,
		float playbackSpeed = 1, Color? tint = null)
	{
		var sprite = new AnimatedSprite2D { SpriteFrames = Frames, Position = Actor.GlobalPosition + offset,
			Scale = new((reverse ? 1 : -1) * Actor.FacingDirection * scale.X * Parameters.VisualScale,
				scale.Y * Parameters.VisualScale), SpeedScale = playbackSpeed * Parameters.EffectSpeed,
			SelfModulate = tint ?? Colors.White, ZIndex = 6 };
		Actor.GetParent().AddChild(sprite);
		sprite.Play(animation);
		var timer = new Timer { WaitTime = lifetime / Parameters.EffectSpeed * Parameters.EffectLifetime, OneShot = true };
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
