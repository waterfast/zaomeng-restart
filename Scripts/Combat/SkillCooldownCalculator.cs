using System;

namespace Zaomeng;

public static class SkillCooldownCalculator
{
	public static float Calculate(float baseSeconds, float hasteRating)
	{
		if (!float.IsFinite(baseSeconds) || baseSeconds < 0 || !float.IsFinite(hasteRating))
			throw new ArgumentOutOfRangeException(nameof(baseSeconds), "技能冷却和极速必须为有限数值，基础冷却不能为负。");
		return baseSeconds / (1 + Math.Max(0, hasteRating) / 100);
	}
}
