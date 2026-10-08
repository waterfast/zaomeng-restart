using Godot;
using Zaomeng.Combat.Buffs;

namespace Zaomeng.Monsters;

/// <summary>低血量化卵，在读秒结束前击破可阻止复生；状态归本次出生。</summary>
public partial class RebirthMechanic : MonsterMechanic
{
	[Export] public BuffDefinition EggState { get; set; } = null!;
	[Export] public float Threshold { get; set; } = 0.2f;
	[Export] public float HealthMultiplier { get; set; } = 1.5f;
	private float _remaining;
	public bool IsEgg => _remaining > 0;
	public override void ResetForSpawn() { _remaining = 0; Actor.Buffs.RemoveSource("mechanic:egg"); }
	public override void _PhysicsProcess(double delta)
	{
		if (Actor.IsDead) { _remaining = 0; return; }
		if (_remaining <= 0)
		{
			if (Actor.Health / Actor.MaxHealth > Threshold) return;
			_remaining = EggState.Duration;
			Actor.CancelSkillBehavior();
			Actor.Buffs.Apply(EggState, "mechanic:egg");
			Actor.Animator.Play(EggState.ForcedAnimation);
			return;
		}
		_remaining -= (float)delta;
		if (_remaining > 0) return;
		Actor.Buffs.RemoveSource("mechanic:egg");
		Actor.MaxHealth *= HealthMultiplier;
		Actor.Heal(Actor.MaxHealth);
		Actor.PhysicalDefense += 10;
		Actor.MagicDefense += 10;
	}
}
