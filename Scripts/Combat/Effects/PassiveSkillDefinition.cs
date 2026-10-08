using System;
using Godot;

namespace Zaomeng.Combat.Effects;

/// <summary>装备与怪物共用的被动定义，界面说明和战斗读取同一组效果参数。</summary>
[GlobalClass]
[Tool]
public partial class PassiveSkillDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string NameKey { get; set; } = "";
	[Export] public string DescriptionKey { get; set; } = "";
	[Export] public Texture2D? Icon { get; set; }
	[Export] public int Priority { get; set; }
	[Export] public int MaximumLevel { get; set; } = 1;
	public Texture2D DisplayIcon => Icon ?? GD.Load<Texture2D>("res://Assets/Art/Skill/default_black.svg");
	[Export] public PassiveEffect Effect { get; set; } = null!;
	public string DisplayName => TranslationServer.Translate(NameKey).ToString();
	public string Description => Effect.FormatDescription(TranslationServer.Translate(DescriptionKey).ToString());

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(NameKey) ||
			string.IsNullOrWhiteSpace(DescriptionKey) || Effect is null || MaximumLevel < 1)
			throw new InvalidOperationException($"被动技能 {Id} 缺少 ID、翻译键或效果资源。");
		Effect.Validate();
	}
}
