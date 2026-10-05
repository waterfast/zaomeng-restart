using Godot;

namespace Zaomeng;

public readonly record struct HitResult(float Damage, Vector2 Knockback, float Hitstun,
	DamageType DamageType = DamageType.Physical, bool Critical = false);

public static class CombatResolver
{
	// 动作资源只保存倍率；角色属性与防御在真正命中时读取。
	public static bool Resolve(CharacterActor attacker, CharacterActor target, HitDefinition hit, int? skillLevel = null,
		SkillDefinition? sourceSkill = null)
	{
		if (attacker == target || attacker.Team == target.Team || attacker.IsDead || target.IsDead || target.IsInvulnerable)
			return false;

		int level = skillLevel ?? attacker.CurrentHitLevel;
		float minimum = hit.MinimumMultiplier(level);
		float maximum = hit.MaximumMultiplier(level);
		float multiplier = Mathf.IsEqualApprox(minimum, maximum)
			? minimum : (float)GD.RandRange(minimum, maximum);
		float power = Mathf.Max(0, attacker.Attack * multiplier + hit.FlatDamage);
		if (attacker is Player damageOwner)
			power = damageOwner.ModifyOutgoingDamage(target, sourceSkill, power);
		LegacyDamageCalculator.Result result = LegacyDamageCalculator.Calculate(attacker, target,
			power, hit.DamageType, hit.CanCrit, GD.Randf(), GD.Randf());
		if (result.Missed)
		{
			target.ReportDodge();
			CombatTextSpawner.ShowMiss(target);
			return true;
		}
		var knockback = new Vector2(hit.Knockback.X * attacker.FacingDirection, hit.Knockback.Y);
		var resolvedHit = new HitResult(result.Damage, knockback, Mathf.Max(0, hit.Hitstun),
			hit.DamageType, result.Critical);
		target.ReceiveHit(resolvedHit);
		if (result.Damage > 0 && attacker is Player hitOwner)
			hitOwner.NotifyHitDealt(target, resolvedHit);
		if (result.Damage > 0 && attacker is Player attackingPlayer && target is Monster)
			attackingPlayer.ConfirmCombatHit();
		// 只在致死命中结算一次；尸体回收、离屏回收均不产生经验。
		if (target.IsDead && target is Monster monster && attacker is Player player)
			Zaomeng.Level.MonsterRewardSpawner.Award(monster, player);
		if (hit.DamageType == DamageType.Physical && attacker.LifeSteal > 0)
			attacker.Heal(result.Damage * attacker.LifeSteal);
		return true;
	}
}
