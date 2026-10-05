using System;
using Godot;
using Zaomeng.UI.Inventory;

namespace Zaomeng.Character;

public enum CharacterStatField
{
	MaxHealth, MaxMana, Attack, Luck, PhysicalDefense, MagicDefense, CriticalRating, DodgeRating,
	HealthRegeneration, ManaRegeneration, Accuracy, Toughness, LifeSteal, ArmorPenetration,
	CriticalResistance, MagicPenetration, HasteRating, SkillLevelBonus
}
public enum StatDisplayFormat { Number, CurrentHealth, CurrentMana, Rating, Percentage }

/// <summary>展示元数据独立注册；不持有角色，不负责数值来源或战斗结算。</summary>
[GlobalClass]
public partial class CharacterStatDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public CharacterStatField Field { get; set; }
	[Export] public string NameKey { get; set; } = "";
	[Export] public string DescriptionKey { get; set; } = "";
	[Export] public StatDisplayFormat Format { get; set; }
	[Export] public float RatingConstant { get; set; } = 100;
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(NameKey) || string.IsNullOrWhiteSpace(DescriptionKey) ||
			!Enum.IsDefined(Field) || !Enum.IsDefined(Format) || !float.IsFinite(RatingConstant) || RatingConstant <= 0)
			throw new InvalidOperationException($"属性 {Id} 的字段、说明或格式无效。");
	}
	public CharacterStatRegistry.Entry CreateEntry() => new(Id, NameKey, Read, FormatValue, DescriptionKey);
	public float Read(CharacterStats stats) => Field switch
	{
		CharacterStatField.MaxHealth => stats.MaxHealth, CharacterStatField.MaxMana => stats.MaxMana,
		CharacterStatField.Attack => stats.Attack, CharacterStatField.Luck => stats.Luck,
		CharacterStatField.PhysicalDefense => stats.PhysicalDefense, CharacterStatField.MagicDefense => stats.MagicDefense,
		CharacterStatField.CriticalRating => stats.CriticalRating, CharacterStatField.DodgeRating => stats.DodgeRating,
		CharacterStatField.HealthRegeneration => stats.HealthRegeneration, CharacterStatField.ManaRegeneration => stats.ManaRegeneration,
		CharacterStatField.Accuracy => stats.Accuracy, CharacterStatField.Toughness => stats.Toughness,
		CharacterStatField.LifeSteal => stats.LifeSteal, CharacterStatField.ArmorPenetration => stats.ArmorPenetration,
		CharacterStatField.CriticalResistance => stats.CriticalResistance, CharacterStatField.MagicPenetration => stats.MagicPenetration,
		CharacterStatField.HasteRating => stats.HasteRating, CharacterStatField.SkillLevelBonus => stats.SkillLevelBonus,
		_ => throw new InvalidOperationException("未知属性字段。")
	};
	private string FormatValue(float value, Player player)
	{
		string number = CharacterStatRegistry.Number(value);
		return Format switch
		{
			StatDisplayFormat.CurrentHealth => $"{CharacterStatRegistry.Number(player.Health)}/{number}",
			StatDisplayFormat.CurrentMana => $"{CharacterStatRegistry.Number(player.Mana)}/{number}",
			StatDisplayFormat.Rating => $"{number}({CharacterStatRegistry.Number(MathF.Round(Math.Max(0, value) / (Math.Max(0, value) + RatingConstant) * 100, 1))}%)",
			StatDisplayFormat.Percentage => $"{CharacterStatRegistry.Number(value * 100)}%",
			_ => number
		};
	}
}
