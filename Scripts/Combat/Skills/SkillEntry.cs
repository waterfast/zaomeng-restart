using System;
using Godot;

namespace Zaomeng.Skills;

/// <summary>学习和显示信息显式引用动作或属性；不通过ID拼接资源路径。</summary>
[GlobalClass]
public partial class SkillEntry : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	[Export] public Texture2D? Icon { get; set; }
	public Texture2D DisplayIcon => Icon ?? GD.Load<Texture2D>("res://Assets/Art/Skill/default_black.svg");
	[Export] public int MaximumLevel { get; set; } = 1;
	[Export] public SkillGrowth Growth { get; set; } = new();
	[Export] public SkillDefinition? Action { get; set; }
	[Export] public Godot.Collections.Array<PassiveSkillBonus> PassiveBonuses { get; set; } = new();
	public bool Passive => Action is null;
	public int LearningCost(int learnedLevel) => (Action?.Growth ?? Growth).LearningCost(learnedLevel);
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName) ||
			MaximumLevel < 1 || Growth is null)
			throw new InvalidOperationException($"技能 {Id} 的名称、图标、等级或学习费用无效。");
		if (PassiveBonuses is null || (Action is null) != (PassiveBonuses.Count > 0))
			throw new InvalidOperationException($"技能 {Id} 必须配置主动动作或被动加成，不能混用。");
		if (Action is { } action && (action.Id != Id || action.Animation.IsEmpty || action.FramesPerSecond <= 0 ||
			!float.IsFinite(action.CooldownSeconds) || action.CooldownSeconds < 0 || action.Growth is null ||
			(action.StartCooldownOnImpact && action.BehaviorScene is null)))
			throw new InvalidOperationException($"技能 {Id} 的动作配置无效或ID不一致。");
		(Action?.Growth ?? Growth).Validate(MaximumLevel);
		Action?.WushuangGain?.Validate();
		foreach (PassiveSkillBonus bonus in PassiveBonuses)
		{
			if (bonus is null) throw new InvalidOperationException($"技能 {Id} 缺少被动加成。");
			bonus.Validate(MaximumLevel);
		}
		if (Action?.BehaviorScene is { } scene)
		{
			Node behavior = scene.Instantiate();
			try
			{
				if (behavior is not SkillBehavior configured) throw new InvalidOperationException($"技能 {Id} 的行为场景必须继承SkillBehavior。");
				configured.ValidateConfiguration();
			}
			finally { behavior.Free(); }
		}
	}
}
