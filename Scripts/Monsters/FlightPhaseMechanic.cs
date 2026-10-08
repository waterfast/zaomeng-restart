using Godot;

namespace Zaomeng.Monsters;

/// <summary>独立形态状态；化卵和死亡时恢复重力，池复用不遗留飞行。</summary>
public partial class FlightPhaseMechanic : MonsterMechanic
{
	[Export] public float GroundSeconds { get; set; } = 9;
	[Export] public float FlightSeconds { get; set; } = 16;
	[Export] public float Altitude { get; set; } = 180;
	private float _remaining;
	private float _groundY;
	private float _gravity = 1000;
	private bool _flying;
	public bool Flying => _flying;
	public override void ResetForSpawn()
	{
		Actor.Gravity = _gravity;
		_flying = false;
		_remaining = GroundSeconds;
		_groundY = Actor.Position.Y;
		SetMode();
	}
	public override void _PhysicsProcess(double delta)
	{
		if (Actor.IsDead || Actor.Buffs.PreventsActions)
		{
			if (_flying) { _flying = false; SetMode(); }
			_remaining = GroundSeconds;
			return;
		}
		_remaining -= (float)delta;
		if (_remaining <= 0 && Actor.State == ActorState.Free)
		{
			_flying = !_flying;
			_remaining = _flying ? FlightSeconds : GroundSeconds;
			if (_flying) _groundY = Actor.Position.Y;
			SetMode();
		}
		if (_flying)
			Actor.Velocity = new(Actor.Velocity.X, Mathf.Clamp((_groundY - Altitude - Actor.Position.Y) * 4, -350, 350));
	}
	private void SetMode()
	{
		Actor.CombatMode = _flying ? "flight" : "ground";
		Actor.Gravity = _flying ? 0 : _gravity;
		Actor.IdleAnimation = _flying ? "Fly_wait" : "wait";
		Actor.MoveAnimation = _flying ? "Fly" : "walk";
		Actor.AirAnimation = _flying ? "Fly" : "jump1";
	}
}
