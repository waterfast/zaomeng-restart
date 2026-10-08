using Godot;

namespace Zaomeng.Monsters;

public partial class HealthPhaseMechanic : MonsterMechanic
{
	[Export] public float Threshold { get; set; } = 0.7f;
	[Export] public StringName HighIdle { get; set; } = "wait_1";
	[Export] public StringName HighMove { get; set; } = "walk_1";
	[Export] public StringName HighHurt { get; set; } = "hurt_1";
	[Export] public StringName LowIdle { get; set; } = "wait_2";
	[Export] public StringName LowMove { get; set; } = "walk_2";
	[Export] public StringName LowHurt { get; set; } = "hurt_2";
	[Export] public MonsterDefinition? LowPhaseSummon { get; set; }
	private bool _summoned;
	public override void ResetForSpawn() { _summoned = false; UpdatePhase(false); }
	public override void _PhysicsProcess(double delta)
	{
		if (!Actor.IsDead) UpdatePhase(Actor.Health / Actor.MaxHealth <= Threshold);
	}
	private void UpdatePhase(bool low)
	{
		if (low && !_summoned && LowPhaseSummon is not null && Actor.Summon is not null)
		{
			_summoned = true;
			Actor.Summon(LowPhaseSummon, 1);
		}
		Actor.IdleAnimation = low ? LowIdle : HighIdle;
		Actor.MoveAnimation = low ? LowMove : HighMove;
		Actor.HurtAnimation = low ? LowHurt : HighHurt;
	}
}
