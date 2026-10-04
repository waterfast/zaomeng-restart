using Godot;

namespace Zaomeng.Level;

/// <summary>旧版灵魂光球：先上浮显现，再加速追踪玩家，吸收时只结算一次。</summary>
public partial class SoulPickup : Node2D
{
	private Player _target = null!;
	private int _value;
	private float _elapsed;
	private float _speed;
	private bool _collected;

	public void Configure(Player target, int value)
	{
		_target = target;
		_value = value;
	}

	public override void _Ready()
	{
		Modulate = new Color(1, 1, 1, 0);
		Tween appear = CreateTween().SetParallel();
		appear.TweenProperty(this, "modulate:a", 1f, 1.5);
		appear.TweenProperty(this, "position", Position + new Vector2(0, -GD.RandRange(20, 40)), 0.4);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_collected || !IsInstanceValid(_target) || !_target.IsInsideTree())
		{
			QueueFree();
			return;
		}
		_elapsed += (float)delta;
		if (_elapsed < 0.6f || _target.IsDead) return;
		// 原版按 60 Hz 每帧加速 0.2、位移乘 0.5；换算成秒避免帧率影响。
		_speed = Mathf.Min(600, _speed + 360 * (float)delta);
		Vector2 destination = _target.GlobalPosition + new Vector2(0, -35);
		GlobalPosition = GlobalPosition.MoveToward(destination, _speed * (float)delta);
		if (GlobalPosition.DistanceTo(destination) > 12) return;
		_collected = _target.TryCollectSouls(_value);
		if (_collected) QueueFree();
	}
}
