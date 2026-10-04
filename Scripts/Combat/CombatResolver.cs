using Godot;

namespace Zaomeng;

public readonly record struct HitResult(float Damage, Vector2 Knockback, float Hitstun,
	DamageType DamageType = DamageType.Physical, bool Critical = false);

public static class CombatResolver
{
	// 动作资源只保存倍率；角色属性与防御在真正命中时读取。
	public static bool Resolve(CharacterActor attacker, CharacterActor target, HitDefinition hit)
	{
		if (attacker == target || attacker.Team == target.Team || attacker.IsDead || target.IsDead)
			return false;

		float minimum = Mathf.Max(0, hit.AttackMultiplier);
		float maximum = hit.AttackMultiplierMax <= 0
			? minimum : Mathf.Max(minimum, hit.AttackMultiplierMax);
		float multiplier = Mathf.IsEqualApprox(minimum, maximum)
			? minimum : (float)GD.RandRange(minimum, maximum);
		multiplier += Mathf.Max(0, attacker.CurrentHitLevel - 1) * hit.MultiplierPerLevel;
		float power = Mathf.Max(0, attacker.Attack * multiplier + hit.FlatDamage);
		LegacyDamageCalculator.Result result = LegacyDamageCalculator.Calculate(attacker, target,
			power, hit.DamageType, hit.CanCrit, GD.Randf(), GD.Randf());
		if (result.Missed)
		{
			CombatTextSpawner.ShowMiss(target);
			return true;
		}
		var knockback = new Vector2(hit.Knockback.X * attacker.FacingDirection, hit.Knockback.Y);
		target.ReceiveHit(new HitResult(result.Damage, knockback, Mathf.Max(0, hit.Hitstun),
			hit.DamageType, result.Critical));
		// 只在致死命中结算一次；尸体回收、离屏回收均不产生经验。
		if (target.IsDead && target is Monster monster && attacker is Player player)
			Zaomeng.Level.MonsterRewardSpawner.Award(monster, player);
		if (hit.DamageType == DamageType.Physical && attacker.LifeSteal > 0)
			attacker.Heal(result.Damage * attacker.LifeSteal);
		return true;
	}
}
