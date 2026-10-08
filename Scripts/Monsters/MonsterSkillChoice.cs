using System;
using Godot;
namespace Zaomeng.Monsters;

/// <summary>数组顺序表示优先级；生命和距离条件让不同模板组合不同战术。</summary>
[GlobalClass]
public partial class MonsterSkillChoice : Resource
{
	[Export] public SkillDefinition Skill { get; set; } = null!;
	[Export] public int Level { get; set; } = 1;
	[Export] public string RequiredMode { get; set; } = "";
	[Export] public float MinimumRange { get; set; }
	[Export] public float MaximumRange { get; set; } = 85;
	[Export] public float MaximumHeightDifference { get; set; } = 65;
	[Export] public float MinimumHealthRatio { get; set; }
	[Export] public float MaximumHealthRatio { get; set; } = 1;
	public void Validate()
	{
		if (Skill is null || Level < 1 || !float.IsFinite(MinimumRange) || MinimumRange < 0
			|| !float.IsFinite(MaximumRange) || MaximumRange < MinimumRange
			|| !float.IsFinite(MaximumHeightDifference) || MaximumHeightDifference < 0
			|| !float.IsFinite(MinimumHealthRatio) || !float.IsFinite(MaximumHealthRatio)
			|| MinimumHealthRatio < 0 || MaximumHealthRatio > 1 || MinimumHealthRatio > MaximumHealthRatio
			|| !float.IsFinite(Skill.CooldownSeconds) || Skill.CooldownSeconds < 0)
			throw new InvalidOperationException("怪物技能选择配置无效。");
	}
}
