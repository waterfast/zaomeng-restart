using System;

namespace Zaomeng.Skills;

public static class SkillLevelResolver
{
	public static int Effective(int learnedLevel, float bonus, int maximumLevel)
	{
		if (learnedLevel <= 0) return 0;
		return (int)Math.Clamp((double)learnedLevel + Math.Floor(Math.Max(0, bonus)), 1, maximumLevel);
	}
}
