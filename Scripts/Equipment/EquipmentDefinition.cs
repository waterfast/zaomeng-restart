using Godot;
using Zaomeng.Character;
using Zaomeng.Items;
using Zaomeng.Equipment.Skills;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Equipment;

/// <summary>装备脚本基类。资源只描述静态数据，不持有角色、背包或存档服务。</summary>
[GlobalClass]
[Tool]
public partial class EquipmentDefinition : ItemDefinition
{
	[Export] public EquipmentSlot Slot { get; set; } = EquipmentSlot.Weapon;
	[Export(PropertyHint.Range, "0,8,1")] public int GemSocketCount { get; set; }
	[Export] public string RequiredCharacterId { get; set; } = "";
	[Export] public int RequiredLevel { get; set; } = 1;
	[Export] public EquipmentRarity Rarity { get; set; }
	[Export] public string Quality { get; set; } = "";
	[Export] public Color QualityColor { get; set; } = Colors.White;
	[Export] public string OwnerCaption { get; set; } = "";
	[Export] public float HealthBonus { get; set; }
	[Export] public float ManaBonus { get; set; }
	[Export] public float PhysicalDefenseBonus { get; set; }
	[Export] public float MagicDefenseBonus { get; set; }
	[Export] public float DodgeBonus { get; set; }
	[Export] public float HealthRegenerationBonus { get; set; }
	[Export] public float ManaRegenerationBonus { get; set; }
	[Export] public float LifeStealBonus { get; set; }
	[Export] public float LuckBonus { get; set; }
	[Export] public float ToughnessBonus { get; set; }
	[Export] public float CriticalResistanceBonus { get; set; }
	[Export] public float ArmorPenetrationBonus { get; set; }
	[Export] public float MagicPenetrationBonus { get; set; }
	[Export] public float HasteBonus { get; set; }
	[Export] public float SkillLevelBonus { get; set; }
	[Export] public Godot.Collections.Array<EquipmentSkillDefinition> GrantedSkills { get; set; } = new();
	[Export] public MagicWeaponPresentation? MagicWeaponPresentation { get; set; }

	public virtual bool CanEquip(SaveCharacter character) => character.Level >= RequiredLevel &&
		(RequiredCharacterId.Length == 0 || RequiredCharacterId == character.Id);

	// 返回独立的数值快照，计算器不会修改或累计到资源自身。
	public virtual CharacterStats GetStatBonuses() => new()
	{
		MaxHealth = HealthBonus, MaxMana = ManaBonus, Attack = Attack,
		PhysicalDefense = PhysicalDefenseBonus, MagicDefense = MagicDefenseBonus,
		CriticalRating = CriticalRating, Accuracy = Accuracy, DodgeRating = DodgeBonus,
		HealthRegeneration = HealthRegenerationBonus, ManaRegeneration = ManaRegenerationBonus,
		LifeSteal = LifeStealBonus, Luck = LuckBonus, Toughness = ToughnessBonus,
		CriticalResistance = CriticalResistanceBonus, ArmorPenetration = ArmorPenetrationBonus,
		MagicPenetration = MagicPenetrationBonus, HasteRating = HasteBonus, SkillLevelBonus = SkillLevelBonus
	};
}
