using System;
using Zaomeng.Items;

namespace Zaomeng.Character;

/// <summary>按来源汇总当前角色属性；装备数据留在物品定义中，不写回基础属性。</summary>
public static class CharacterStatCalculator
{
	public static CharacterStats Calculate(Character character, ItemCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(character);
		ArgumentNullException.ThrowIfNull(catalog);
		var result = new CharacterStats();
		Add(result, character.BaseStats);
		Add(result, character.PermanentBonuses);

		EquipmentLoadout equipment = character.Equipment;
		foreach (string id in new[] { equipment.WeaponId, equipment.ArmorId,
			equipment.AccessoryId, equipment.WingId, equipment.TitleId,
			equipment.CostumeId, equipment.MagicWeaponId })
		{
			if (string.IsNullOrEmpty(id) || !catalog.TryGetDefinition(id, out ItemDefinition? item)
				|| item?.Category != ItemCategory.Equipment)
				continue;
			// 当前目录只迁入六件武器；它们的旧版有效属性是攻击、暴击值和命中值。
			result.Attack += item.Attack;
			result.CriticalRating += item.CriticalRating;
			result.Accuracy += item.Accuracy;
		}
		return result;
	}

	public static bool SameValues(CharacterStats left, CharacterStats right) =>
		left.MaxHealth == right.MaxHealth && left.MaxMana == right.MaxMana &&
		left.Attack == right.Attack && left.PhysicalDefense == right.PhysicalDefense &&
		left.MagicDefense == right.MagicDefense && left.CriticalRating == right.CriticalRating &&
		left.DodgeRating == right.DodgeRating && left.HealthRegeneration == right.HealthRegeneration &&
		left.ManaRegeneration == right.ManaRegeneration && left.LifeSteal == right.LifeSteal &&
		left.Luck == right.Luck && left.Toughness == right.Toughness &&
		left.Accuracy == right.Accuracy && left.CriticalResistance == right.CriticalResistance &&
		left.ArmorPenetration == right.ArmorPenetration &&
		left.MagicPenetration == right.MagicPenetration;

	private static void Add(CharacterStats total, CharacterStats bonus)
	{
		total.MaxHealth += bonus.MaxHealth;
		total.MaxMana += bonus.MaxMana;
		total.Attack += bonus.Attack;
		total.PhysicalDefense += bonus.PhysicalDefense;
		total.MagicDefense += bonus.MagicDefense;
		total.CriticalRating += bonus.CriticalRating;
		total.DodgeRating += bonus.DodgeRating;
		total.HealthRegeneration += bonus.HealthRegeneration;
		total.ManaRegeneration += bonus.ManaRegeneration;
		total.LifeSteal += bonus.LifeSteal;
		total.Luck += bonus.Luck;
		total.Toughness += bonus.Toughness;
		total.Accuracy += bonus.Accuracy;
		total.CriticalResistance += bonus.CriticalResistance;
		total.ArmorPenetration += bonus.ArmorPenetration;
		total.MagicPenetration += bonus.MagicPenetration;
	}
}
