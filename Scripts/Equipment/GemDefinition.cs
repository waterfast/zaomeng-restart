using System;
using Godot;
using Zaomeng.Character;
using Zaomeng.Items;

namespace Zaomeng.Equipment;

public enum GemAttribute
{
	Health, Mana, Attack, PhysicalDefense, MagicDefense, Critical, Dodge,
	HealthRegeneration, ManaRegeneration, Accuracy, Luck, Toughness,
	ArmorPenetration, MagicPenetration, LifeSteal, CriticalResistance, Haste, SkillLevel
}

/// <summary>宝石是可堆叠物品；镶嵌后定义 ID 留在装备实例中，效果仍由资源提供。</summary>
[GlobalClass]
public partial class GemDefinition : ItemDefinition
{
	[Export] public GemAttribute Attribute { get; set; } = GemAttribute.Attack;
	[Export] public float Bonus { get; set; }

	public GemDefinition() { Category = ItemCategory.Material; MaxStack = 99; }

	public CharacterStats GetStatBonuses()
	{
		var result = new CharacterStats();
		switch (Attribute)
		{
			case GemAttribute.Health: result.MaxHealth = Bonus; break;
			case GemAttribute.Mana: result.MaxMana = Bonus; break;
			case GemAttribute.Attack: result.Attack = Bonus; break;
			case GemAttribute.PhysicalDefense: result.PhysicalDefense = Bonus; break;
			case GemAttribute.MagicDefense: result.MagicDefense = Bonus; break;
			case GemAttribute.Critical: result.CriticalRating = Bonus; break;
			case GemAttribute.Dodge: result.DodgeRating = Bonus; break;
			case GemAttribute.HealthRegeneration: result.HealthRegeneration = Bonus; break;
			case GemAttribute.ManaRegeneration: result.ManaRegeneration = Bonus; break;
			case GemAttribute.Accuracy: result.Accuracy = Bonus; break;
			case GemAttribute.Luck: result.Luck = Bonus; break;
			case GemAttribute.Toughness: result.Toughness = Bonus; break;
			case GemAttribute.ArmorPenetration: result.ArmorPenetration = Bonus; break;
			case GemAttribute.MagicPenetration: result.MagicPenetration = Bonus; break;
			case GemAttribute.LifeSteal: result.LifeSteal = Bonus; break;
			case GemAttribute.CriticalResistance: result.CriticalResistance = Bonus; break;
			case GemAttribute.Haste: result.HasteRating = Bonus; break;
			case GemAttribute.SkillLevel: result.SkillLevelBonus = Bonus; break;
			default: throw new InvalidOperationException("未知的宝石属性类型。");
		}
		return result;
	}
}
