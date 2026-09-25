using Godot;

namespace Zaomeng;

public readonly record struct HitResult(float Damage, Vector2 Knockback, float Hitstun);

public static class CombatResolver
{
	// Animation decides when; this is the single entry point for damage rules.
	public static bool Resolve(CharacterActor attacker, CharacterActor target, HitDefinition hit)
	{
		if (attacker == target || attacker.Team == target.Team || attacker.IsDead || target.IsDead)
			return false;

		var knockback = new Vector2(hit.Knockback.X * attacker.FacingDirection, hit.Knockback.Y);
		target.ReceiveHit(new HitResult(Mathf.Max(0, hit.Damage), knockback, Mathf.Max(0, hit.Hitstun)));
		return true;
	}
}
