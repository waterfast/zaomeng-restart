namespace Zaomeng.Character;

/// <summary>角色自身的基础或永久加成属性；装备、Buff 等来源以后分别结算。</summary>
public sealed class CharacterStats
{
	public float MaxHealth { get; set; }
	public float MaxMana { get; set; }
	public float Attack { get; set; }
	public float PhysicalDefense { get; set; }
	public float MagicDefense { get; set; }
	// 旧版暴击、闪避和幸运是属性值，实际概率由战斗公式换算。
	public float CriticalRating { get; set; }
	public float DodgeRating { get; set; }
	public float HealthRegeneration { get; set; }
	public float ManaRegeneration { get; set; }
	public float LifeSteal { get; set; }
	public float Luck { get; set; }
	public float Toughness { get; set; }
	public float Accuracy { get; set; }
	public float CriticalResistance { get; set; }
	public float ArmorPenetration { get; set; }
	public float MagicPenetration { get; set; }
	public float HasteRating { get; set; }
	public float SkillLevelBonus { get; set; }
}
