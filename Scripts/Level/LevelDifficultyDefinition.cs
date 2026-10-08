using Godot;
using Zaomeng.Monsters;

namespace Zaomeng.Level;

/// <summary>一次挑战的强度配置，与地图、Boss 等关卡内容独立。</summary>
[GlobalClass]
public partial class LevelDifficultyDefinition : Resource
{
	[Export] public string DisplayName { get; set; } = "普通";
	[Export] public float MonsterHealthMultiplier { get; set; } = 1;
	[Export] public float MonsterAttackMultiplier { get; set; } = 1;
	[Export] public float MonsterDefenseMultiplier { get; set; } = 1;
	[Export] public int MonsterLevelOverride { get; set; }
	[Export] public float BossHealthMultiplier { get; set; } = 1;
	[Export] public float BossAttackMultiplier { get; set; } = 1;
	[Export] public float BossDefenseMultiplier { get; set; } = 1;
	[Export] public int BossLevelOverride { get; set; }
	[Export] public int RecommendedLevelOverride { get; set; }
	[Export] public MonsterDropTable? ExtraMonsterDrops { get; set; }
	[Export] public MonsterDropTable? ExtraBossDrops { get; set; }
	public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName)
		&& Positive(MonsterHealthMultiplier) && Positive(MonsterAttackMultiplier) && Positive(MonsterDefenseMultiplier)
		&& Positive(BossHealthMultiplier) && Positive(BossAttackMultiplier) && Positive(BossDefenseMultiplier)
		&& MonsterLevelOverride >= 0 && BossLevelOverride >= 0 && RecommendedLevelOverride >= 0;

	public float HealthMultiplier(bool boss) => boss ? BossHealthMultiplier : MonsterHealthMultiplier;
	public float AttackMultiplier(bool boss) => boss ? BossAttackMultiplier : MonsterAttackMultiplier;
	public float DefenseMultiplier(bool boss) => boss ? BossDefenseMultiplier : MonsterDefenseMultiplier;
	public int ResolveLevel(bool boss, int templateLevel)
	{
		int configured = boss ? BossLevelOverride : MonsterLevelOverride;
		return configured > 0 ? configured : templateLevel;
	}
	public MonsterDropTable? ExtraDrops(bool boss) => boss ? ExtraBossDrops : ExtraMonsterDrops;
	private static bool Positive(float value) => float.IsFinite(value) && value > 0;
}
