using Godot;

namespace Zaomeng;

public enum DamageType { Physical, Magic, True }

/// <summary>静态的一击配方；实际伤害在命中时根据攻击者、目标和技能等级计算。</summary>
[GlobalClass]
public partial class HitDefinition : Resource
{
	[Export] public float AttackMultiplier { get; set; } = 1;
	// 0 表示固定使用 AttackMultiplier；非零时在最小和最大倍率之间随机。
	[Export] public float AttackMultiplierMax { get; set; }
	[Export] public float MultiplierPerLevel { get; set; }
	[Export] public float FlatDamage { get; set; }
	[Export] public DamageType DamageType { get; set; }
	[Export] public bool CanCrit { get; set; } = true;
	[Export] public Vector2 Knockback { get; set; } = new(60, 0);
	[Export] public float Hitstun { get; set; } = 0.22f;
	public float MinimumMultiplier(int level) => Mathf.Max(0, AttackMultiplier) + Mathf.Max(0, level - 1) * MultiplierPerLevel;
	public float MaximumMultiplier(int level) => (AttackMultiplierMax <= 0 ? Mathf.Max(0, AttackMultiplier) :
		Mathf.Max(AttackMultiplier, AttackMultiplierMax)) + Mathf.Max(0, level - 1) * MultiplierPerLevel;
}
