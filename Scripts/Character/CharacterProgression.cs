using System;

namespace Zaomeng.Character;

/// <summary>旧版角色的等级成长与经验门槛；基础属性由角色 ID 和等级推导。</summary>
public static class CharacterProgression
{
	public const int MaxLevel = 55;

	// 旧数组的第 20 项为 5000，但旧条件仅在等级小于 20 时查表，实际不会用到它。
	private static readonly int[] ExperienceThresholds =
	[
		140, 160, 180, 200, 220, 300, 400, 500, 600, 700,
		800, 900, 1200, 1400, 1600, 2000, 2400, 3000, 4000
	];

	public static long ExperienceToNextLevel(int level)
	{
		if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
		return level < 20 ? ExperienceThresholds[level - 1] : 5000L + 5000L * (level - 19);
	}

	public static bool TryCalculateBaseStats(string characterId, int level, out CharacterStats stats)
	{
		if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
		(int health, int mana, int attack, int defense, int healthGrowth, int manaGrowth,
			int attackGrowth) = characterId switch
		{
			"role_1" => (80, 50, 8, 10, 50, 15, 4),
			"role_2" => (50, 100, 15, 10, 30, 30, 6),
			"role_3" => (120, 25, 15, 15, 60, 10, 4),
			"role_4" => (70, 70, 15, 5, 40, 20, 4),
			"role_5" => (80, 50, 10, 8, 40, 20, 4),
			_ => default
		};
		if (health == 0)
		{
			stats = new CharacterStats();
			return false;
		}

		int gainedLevels = level - 1;
		stats = new CharacterStats
		{
			MaxHealth = health + healthGrowth * gainedLevels,
			MaxMana = mana + manaGrowth * gainedLevels,
			Attack = attack + attackGrowth * gainedLevels,
			PhysicalDefense = defense + gainedLevels,
			MagicDefense = defense + gainedLevels
		};
		return true;
	}

	public static bool SyncBaseStats(Character character)
	{
		ArgumentNullException.ThrowIfNull(character);
		if (!TryCalculateBaseStats(character.Id, character.Level, out CharacterStats calculated))
			return false;
		if (CharacterStatCalculator.SameValues(character.BaseStats, calculated))
			return false;
		character.BaseStats = calculated;
		return true;
	}
}
