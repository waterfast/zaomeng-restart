using Godot;

namespace Zaomeng.Level;

/// <summary>地图入口和关卡运行时共同使用的内容定义。</summary>
[GlobalClass]
public partial class LevelDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string Difficulty { get; set; } = "普通";
	[Export] public string Description { get; set; } = "";
	[Export] public int RecommendedLevel { get; set; } = 1;
	[Export] public int ProgressLevel { get; set; } = 1;
	[Export(PropertyHint.File, "*.tscn")] public string LevelScenePath { get; set; } = "";
	[Export] public PackedScene? MapScene { get; set; }
	[Export] public Texture2D? PreviewBackground { get; set; }
	[Export] public Godot.Collections.Array<LevelMonsterPreview> Monsters { get; set; } = new();
	[Export] public string[] CommonDropIds { get; set; } = [];
	[Export] public string[] BossDropIds { get; set; } = [];
	[Export] public WaveEncounterDefinition? WaveEncounter { get; set; }
}
