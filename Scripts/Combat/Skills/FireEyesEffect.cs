using Godot;

namespace Zaomeng.Skills;

public partial class FireEyesEffect : Node2D
{
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public HitDefinition Hit { get; set; } = null!;
	[Export] public int HitCount { get; set; } = 3;
	[Export] public float Interval { get; set; } = 1.1f;
	private Player _player = null!;
	private Monster _target = null!;
	private AnimatedSprite2D _sprite = null!;
	private float _time;
	private int _hits;
	private int _level;
	public void Configure(Player player, Monster target, int? level = null) { _player = player; _target = target; _level = level ?? player.CurrentHitLevel; }
	public override void _Ready()
	{
		_sprite = new AnimatedSprite2D { SpriteFrames = Frames };
		AddChild(_sprite);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_player) || !IsInstanceValid(_target) || _player.IsDead || _target.IsDead || !_target.Visible)
		{ QueueFree(); return; }
		GlobalPosition = _target.GlobalPosition + new Vector2(0, -40);
		_time += (float)delta;
		if (_hits < HitCount && _time >= _hits * Interval)
		{
			_hits++;
			_sprite.Play("hyjj");
			_sprite.SetFrameAndProgress(0, 0);
			CombatResolver.Resolve(_player, _target, Hit, _level);
		}
		if (_time >= HitCount * Interval) QueueFree();
	}
}
