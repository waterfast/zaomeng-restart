using System;
using Godot;

namespace Zaomeng;

/// <summary>旧版角色与怪物的命中、等级压制、暴击和防御结算。</summary>
public static class LegacyDamageCalculator
{
	public readonly record struct Result(int Damage, bool Missed, bool Critical);

	public static Result Calculate(CharacterActor attacker, CharacterActor target,
		float power, DamageType type, bool canCrit, float missRoll, float critRoll)
	{
		bool attackingPlayer = attacker is Player;
		float miss = MathF.Max(0, target.DodgeRating - attacker.Accuracy);
		float crit = MathF.Max(0, attacker.CriticalRating - target.CriticalResistance);
		float luck = MathF.Max(0, attacker.Luck - target.Toughness);
		int difference = Math.Abs(attacker.Level - target.Level);
		float damage = MathF.Max(0, power);

		if (attackingPlayer)
		{
			// 旧版对玩家进攻和怪物进攻分别使用了不同的等级压制系数。
			bool targetHigher = target.Level > attacker.Level;
			miss *= 1 + (targetHigher ? 1 : -1) * MathF.Min(difference * 0.04f, 0.9f);
			crit *= 1 + (targetHigher ? -1 : 1) * MathF.Min(difference * 0.11f, 1);
			luck *= 1 + (targetHigher ? -1 : 1) * MathF.Min(difference * 0.07f, 0.7f);
			damage = MathF.Truncate(damage * (targetHigher
				? 1 - Math.Min(difference, 5) * 0.05f
				: 1 + Math.Min(difference, 2) * 0.05f));
		}
		else if (target is Player)
		{
			bool monsterLower = attacker.Level < target.Level;
			float sign = monsterLower ? -1 : 1;
			miss *= 1 + sign * MathF.Min(difference * 0.03f, 1);
			crit *= 1 + sign * MathF.Min(difference * 0.07f, 1);
			luck *= 1 + sign * MathF.Min(difference * 0.07f, 0.7f);
			damage = MathF.Truncate(damage * (1 + sign * Math.Min(difference, 5) * 0.05f));
		}

		float missDenominator = attackingPlayer ? 70 : 100;
		if (missRoll < RatingChance(miss, missDenominator))
			return new Result(0, true, false);

		bool critical = canCrit && critRoll < RatingChance(crit, 100);
		if (critical)
		{
			float luckDenominator = MathF.Max(1, target.CriticalLuckConstant);
			damage *= 2 + RatingChance(luck, luckDenominator);
		}

		float defense = type switch
		{
			DamageType.Physical => MathF.Max(0, target.PhysicalDefense - attacker.ArmorPenetration),
			DamageType.Magic => MathF.Max(0, target.MagicDefense - attacker.MagicPenetration),
			_ => 0
		};
		if (type != DamageType.True)
		{
			float defenseConstant = MathF.Max(1, target.DefenseConstant);
			damage = MathF.Truncate(damage * (1 - RatingChance(defense, defenseConstant)));
		}
		return new Result(Math.Max(0, (int)damage), false, critical);
	}

	private static float RatingChance(float rating, float denominator)
	{
		if (rating <= 0) return 0;
		return MathF.Round(rating / (rating + denominator), 3, MidpointRounding.AwayFromZero);
	}
}
