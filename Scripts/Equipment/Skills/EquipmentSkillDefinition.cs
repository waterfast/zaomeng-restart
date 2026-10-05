using System;
using Godot;

namespace Zaomeng.Equipment.Skills;

/// <summary>独立游戏数据；装备只引用定义，界面和战斗共用同一组效果参数。</summary>
[GlobalClass]
public partial class EquipmentSkillDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string NameKey { get; set; } = "";
	[Export] public string DescriptionKey { get; set; } = "";
	[Export] public Texture2D? Icon { get; set; }
	[Export] public int MaximumLevel { get; set; } = 1;
	public Texture2D DisplayIcon => Icon ?? GD.Load<Texture2D>("res://Assets/Art/Skill/default_black.svg");
	[Export] public EquipmentSkillEffect Effect { get; set; } = null!;
	public string DisplayName => TranslationServer.Translate(NameKey).ToString();
	public string Description => Effect.FormatDescription(TranslationServer.Translate(DescriptionKey).ToString());

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(NameKey) ||
			string.IsNullOrWhiteSpace(DescriptionKey) || Effect is null || MaximumLevel < 1)
			throw new InvalidOperationException($"装备技能 {Id} 缺少 ID、翻译键或效果资源。");
		Effect.Validate();
	}
}
