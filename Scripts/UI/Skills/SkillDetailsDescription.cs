using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zaomeng.Character;
using Zaomeng.Skills;

namespace Zaomeng.UI;

/// <summary>数值读取施放和属性计算所用的数据，不从正文提取公式。</summary>
public static class SkillDetailsDescription
{
	public static string Describe(SkillEntry skill, int effectiveLevel, float haste)
	{
		int level = Math.Clamp(effectiveLevel, 1, skill.MaximumLevel);
		var lines = new List<string> { effectiveLevel == 0 ? "未学习 · 1级预览" : $"当前生效等级：{level}" };
		if (skill.Action is { } action)
		{
			lines.Add($"魔耗：{action.GetManaCost(level)}");
			lines.Add($"基础冷却：{action.CooldownSeconds:0.###}秒");
			lines.Add($"极速后冷却：{SkillCooldownCalculator.Calculate(action.CooldownSeconds, haste):0.###}秒" +
				(action.StartCooldownOnImpact ? "（落地后开始）" : "（施放时开始）"));
			int segment = 0;
			foreach (HitDefinition hit in action.Hits.Select(window => window.Hit).OfType<HitDefinition>().Distinct())
			{
				segment++;
				float minimum = hit.MinimumMultiplier(level), maximum = hit.MaximumMultiplier(level);
				string multiplier = minimum == maximum ? $"{minimum:0.##}" : $"{minimum:0.##}～{maximum:0.##}";
				string damageType = hit.DamageType switch { DamageType.Physical => "物理", DamageType.Magic => "魔法", _ => "真实" };
				lines.Add($"伤害{segment}：攻击×{multiplier}" + (hit.FlatDamage == 0 ? "" : $" + {hit.FlatDamage:0.##}") + $"（{damageType}）");
			}
			if (action.BehaviorScene is { } scene)
			{
				var behavior = scene.Instantiate<SkillBehavior>();
				try
				{
					string values = behavior.DescribeValues(level);
					if (values.Length > 0) lines.Add(values);
				}
				finally { behavior.Free(); }
			}
		}
		else
		{
			var catalog = CharacterStatCatalog.Default;
			foreach (PassiveSkillBonus bonus in skill.PassiveBonuses)
			{
				CharacterStatField field = bonus.Stat switch
				{
					PassiveStat.CriticalRating => CharacterStatField.CriticalRating,
					PassiveStat.HealthRegeneration => CharacterStatField.HealthRegeneration,
					PassiveStat.ManaRegeneration => CharacterStatField.ManaRegeneration,
					PassiveStat.LifeSteal => CharacterStatField.LifeSteal,
					PassiveStat.Haste => CharacterStatField.HasteRating,
					_ => throw new InvalidOperationException("未登记的被动属性。")
				};
				CharacterStatDefinition definition = catalog.Definitions.First(entry => entry.Field == field);
				float value = bonus.ValueAtLevel(level);
				string amount = definition.Format == StatDisplayFormat.Percentage ? $"{value * 100:0.##}%" : $"{value:0.##}";
				string unit = field is CharacterStatField.HealthRegeneration or CharacterStatField.ManaRegeneration ? "/秒" : "";
				lines.Add($"{TranslationServer.Translate(definition.NameKey)}：+{amount}{unit}");
			}
		}
		return string.Join("\n", lines);
	}
}
