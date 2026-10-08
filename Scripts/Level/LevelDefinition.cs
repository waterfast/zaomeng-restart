using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using Zaomeng.Monsters;
using Zaomeng.Items;

namespace Zaomeng.Level;

/// <summary>地图入口和关卡运行时共同使用的内容定义。</summary>
[GlobalClass]
public partial class LevelDefinition : Resource
{
	[Export] public Zaomeng.Audio.MusicTrack? Music { get; set; }
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public Godot.Collections.Array<LevelDifficultyDefinition> Difficulties { get; set; } = new();
	[Export] public string Description { get; set; } = "";
	[Export] public int RecommendedLevel { get; set; } = 1;
	[Export] public int ProgressLevel { get; set; } = 1;
	// 副本复用入口解锁条件，但胜利不能推进主线。
	[Export] public bool AdvancesCampaign { get; set; } = true;
	[Export(PropertyHint.File, "*.tscn")] public string LevelScenePath { get; set; } = "";
	[Export] public PackedScene? MapScene { get; set; }
	[Export] public Texture2D? PreviewBackground { get; set; }
	[Export] public Godot.Collections.Array<LevelMonsterPreview> Monsters { get; set; } = new();
	public string[] CommonDropIds => DropIds(false);
	public string[] BossDropIds => DropIds(true);
	[Export] public WaveEncounterDefinition? WaveEncounter { get; set; }
	[Export] public Vector2 PlayerSpawn { get; set; } = new(380, 490);
	[Export] public int CameraRight { get; set; } = 5300;
	[Export] public int CameraBottom { get; set; } = 650;
	[Export] public Vector2 ExitPosition { get; set; } = new(4400, 500);
	[Export] public bool MapOnly { get; set; }
	private string[] DropIds(bool boss) => WaveEncounter?.Waves.SelectMany(w => w.Monsters)
		.Where(m => m.IsBoss == boss).SelectMany(m => m.Drops?.PossibleItemIds ?? Enumerable.Empty<string>())
		.Distinct().ToArray() ?? [];

	/// <summary>原表与模式额外表分别投掷，UI和击杀奖励共用此入口。</summary>
	public IEnumerable<MonsterDropTable> GetDropTables(MonsterDefinition monster, LevelDifficultyDefinition? difficulty)
	{
		if (monster.Drops is { } original) yield return original;
		if (monster.RecoveryDrops is { } recovery) yield return recovery;
		if (difficulty?.ExtraDrops(monster.IsBoss) is { } extra) yield return extra;
	}

	public string[] GetPossibleDropIds(LevelDifficultyDefinition? difficulty) => WaveEncounter?.Waves
		.SelectMany(wave => wave.Monsters).Distinct()
		.SelectMany(monster => GetDropTables(monster, difficulty))
		.SelectMany(table => table.PossibleItemIds).Distinct().ToArray() ?? [];
	public void Validate(ItemCatalog catalog)
	{
		if (string.IsNullOrWhiteSpace(Id) || MapScene is null || ProgressLevel < 1 || !PlayerSpawn.IsFinite()
			|| !ExitPosition.IsFinite() || CameraRight < 960 || CameraBottom < 540 || Difficulties.Count == 0
			|| Difficulties.Any(d => d is null || !d.IsValid) || (!MapOnly && WaveEncounter is null))
			throw new InvalidOperationException($"关卡 {Id} 配置不完整。");
		foreach (var difficulty in Difficulties)
		{
			ValidateDrops(difficulty.ExtraMonsterDrops, catalog);
			ValidateDrops(difficulty.ExtraBossDrops, catalog);
		}
		if (WaveEncounter is not { } encounter) return;
		if (encounter.Waves.Count == 0) throw new InvalidOperationException($"{Id} 缺少波次。");
		var validated = new System.Collections.Generic.HashSet<MonsterDefinition>();
		var ids = new System.Collections.Generic.Dictionary<string, MonsterDefinition>();
		foreach (var wave in encounter.Waves)
		{
			if (wave is null || wave.Monsters.Count == 0 || wave.LivingLimit < 1 || !float.IsFinite(wave.SpawnInterval)
				|| wave.SpawnInterval <= 0 || !wave.SpawnRange.IsFinite() || wave.SpawnRange.X > wave.SpawnRange.Y
				|| wave.CameraRight < 960 || !float.IsFinite(wave.EntranceX) || !float.IsFinite(wave.GateX)
				|| !float.IsFinite(wave.SpawnRayTop) || !float.IsFinite(wave.SpawnRayBottom)
				|| wave.SpawnRayBottom <= wave.SpawnRayTop || !float.IsFinite(wave.MinimumGroundNormal)
				|| wave.MinimumGroundNormal is < 0 or > 1)
				throw new InvalidOperationException($"{Id} 波次配置无效。");
			foreach (var monster in wave.Monsters)
			{
				if (monster is null) throw new InvalidOperationException($"{Id} 波次缺少怪物模板。");
				if (!validated.Add(monster)) continue;
				if (ids.TryGetValue(monster.Id, out var existing) && existing != monster)
					throw new InvalidOperationException($"怪物模板 ID 重复：{monster.Id}");
				ids[monster.Id] = monster;
				monster.Validate();
				ValidateDrops(monster.Drops, catalog);
				ValidateDrops(monster.RecoveryDrops, catalog);
			}
		}
	}

	private static void ValidateDrops(MonsterDropTable? drops, ItemCatalog catalog)
	{
		if (drops is null) return;
		drops.Validate();
		foreach (var drop in drops.Entries)
			if (!catalog.TryGetDefinition(drop.ItemId, out _))
				throw new InvalidOperationException($"掉落物品未注册：{drop.ItemId}");
	}
}
