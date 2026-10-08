using Godot;
using Zaomeng.Combat.Buffs;

namespace Zaomeng.Monsters;

public partial class StoneShieldMechanic : MonsterMechanic
{
	[Export] public BuffDefinition Shield { get; set; } = null!;
	[Export] public float VulnerableSeconds { get; set; } = 5;
	private float _vulnerable;
	private bool _shielded;
	public bool Shielded => _shielded;
	public Vector2 WeakPoint => Actor.GlobalPosition + new Vector2(-250, -35);
	public override void ResetForSpawn()
	{
		_vulnerable = 0;
		_shielded = true;
		Actor.Buffs.Apply(Shield, "mechanic:shield", permanent: true);
		QueueRedraw();
	}
	public override void _PhysicsProcess(double delta)
	{
		if (Actor.IsDead) { Hide(); return; }
		Show();
		if (!_shielded)
		{
			_vulnerable -= (float)delta;
			if (_vulnerable <= 0) ResetForSpawn();
			return;
		}
		foreach (var node in GetTree().GetNodesInGroup("combat_actors"))
			if (node is Player { IsDead: false } player && player.GlobalPosition.DistanceTo(WeakPoint + new Vector2(0, 35)) < 45)
			{
				_shielded = false;
				_vulnerable = VulnerableSeconds;
				Actor.Buffs.RemoveSource("mechanic:shield");
				QueueRedraw();
				break;
			}
	}
	public override void _Draw()
	{
		if (!_shielded) return;
		DrawArc(new(0, -50), 90, 0, Mathf.Tau, 48, new(0.3f, 0.85f, 1, 0.7f), 4, true);
		DrawCircle(new(-250, -35), 18, new(1, 0.15f, 0.1f, 0.9f));
		DrawArc(new(-250, -35), 25, 0, Mathf.Tau, 32, Colors.Gold, 3, true);
	}
}
