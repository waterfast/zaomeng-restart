using Godot;

namespace Zaomeng;

public readonly record struct HitResult(float Damage, Vector2 Knockback, float Hitstun,
	DamageType DamageType = DamageType.Physical, bool Critical = false);

public static class CombatResolver
{
	// 动作资源只保存倍率；角色属性与防御在真正命中时读取。
	public static bool Resolve(CharacterActor attacker, CharacterActor target, HitDefinition hit, int? skillLevel = null,
		SkillDefinition? sourceSkill = null, int? wushuangGain = null)
	{
		if (attacker == target || attacker.Team == target.Team || attacker.IsDead || target.IsDead || target.IsInvulnerable)
			return false;

		// 旧版在扣血前获取，闪避、零伤害和致死接触仍增加；不能放在伤害通知里。
		if (attacker is Player gainOwner && target is Monster)
			gainOwner.Wushuang.Gain(wushuangGain ?? gainOwner.Wushuang.SampleGain(hit, sourceSkill));

		int level = skillLevel ?? attacker.CurrentHitLevel;
		float minimum = hit.MinimumMultiplier(level);
		float maximum = hit.MaximumMultiplier(level);
		float multiplier = Mathf.IsEqualApprox(minimum, maximum)
			? minimum : (float)GD.RandRange(minimum, maximum);
		float power = Mathf.Max(0, attacker.Attack * multiplier + hit.FlatDamage);
		power = attacker.PassiveEffects.ModifyOutgoingDamage(target, sourceSkill, power);
		power *= attacker.Buffs.OutgoingMultiplier;
		LegacyDamageCalculator.Result result = LegacyDamageCalculator.Calculate(attacker, target,
			power, hit.DamageType, hit.CanCrit, GD.Randf(), GD.Randf());
		if (result.Missed)
		{
			target.ReportDodge();
			CombatTextSpawner.ShowMiss(target);
			return true;
		}
		var knockback = new Vector2(hit.Knockback.X * attacker.FacingDirection, hit.Knockback.Y);
		var resolvedHit = new HitResult(result.Damage * attacker.Buffs.FinalDamageMultiplier, knockback, Mathf.Max(0, hit.Hitstun),
			hit.DamageType, result.Critical);
		// 已通过接触校验；即使零伤害或被护盾完全挡住，也须消耗该窗口。
		// 否则攻击框/弹体会在后续帧重复接触，同一击反复获取无双。
		if (target.ReceiveHit(resolvedHit) is not { } received) return true;
		resolvedHit = received;
		if (resolvedHit.Damage > 0 && !target.IsDead && hit.AppliedBuff is { } buff)
			target.Buffs.Apply(buff, "hit:" + buff.Id, sourceSkill: sourceSkill, sourceActor: attacker);
		if (result.Damage > 0) attacker.PassiveEffects.OnHitDealt(target, resolvedHit);
		if (result.Damage > 0) Zaomeng.Audio.AudioManager.Instance?.PlayEffect(attacker.SoundProfile?.Impact);
		if (result.Damage > 0 && attacker is Player hitOwner)
			hitOwner.NotifyHitDealt(target, resolvedHit);
		if (result.Damage > 0 && attacker is Player attackingPlayer && target is Monster)
			attackingPlayer.ConfirmCombatHit();
		// 只在致死命中结算一次；尸体回收、离屏回收均不产生经验。
		if (target.IsDead && target is Monster monster && attacker is Player player)
			Zaomeng.Level.MonsterRewardSpawner.Award(monster, player);
		if (hit.DamageType == DamageType.Physical && attacker.LifeSteal > 0)
			attacker.Heal(resolvedHit.Damage * attacker.LifeSteal);
		return true;
	}
}
