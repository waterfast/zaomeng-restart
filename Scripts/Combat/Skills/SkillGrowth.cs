using System;
using Godot;

namespace Zaomeng.Skills;

/// <summary>独立策划成长数据；UI、学习费用和施放蓝耗共用，不混入动作脚本。</summary>
[GlobalClass]
[Tool]
public partial class SkillGrowth : Resource
{
	[Export] public int InitialLearningCost { get; set; } = 100;
	[Export] public int LearningCostPerLevel { get; set; } = 500;
	[Export] public int InitialManaCost { get; set; }
	[Export] public int ManaLinearGrowth { get; set; }
	[Export] public int ManaQuadraticGrowth { get; set; }
	public int LearningCost(int learnedLevel) => checked(InitialLearningCost + Math.Max(0, learnedLevel) * LearningCostPerLevel);
	public int ManaCost(int level)
	{
		int extra = Math.Max(0, level - 1);
		return checked(InitialManaCost + extra * ManaLinearGrowth + extra * extra * ManaQuadraticGrowth);
	}
	public void Validate(int maximumLevel)
	{
		if (InitialLearningCost <= 0 || LearningCostPerLevel < 0 || InitialManaCost < 0 || ManaLinearGrowth < 0 || ManaQuadraticGrowth < 0)
			throw new InvalidOperationException("技能成长的费用或魔耗不能为负，初始费用必须大于零。");
		try { LearningCost(maximumLevel - 1); ManaCost(maximumLevel); }
		catch (OverflowException error) { throw new InvalidOperationException("技能成长超出可计算范围。", error); }
	}
}
