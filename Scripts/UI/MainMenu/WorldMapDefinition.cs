using Godot;

namespace Zaomeng.UI.MainMenu;

/// <summary>地图的入口配置，关卡内容和开启条件不写进按钮控制器。</summary>
[GlobalClass]
public partial class WorldMapDefinition : Resource
{
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "";
	[Export] public bool HasPlayableLevels { get; set; }
	[Export] public Godot.Collections.Array<LevelEntranceDefinition> LevelEntrances { get; set; } = new();
	[Export] public NodePath SkillButton { get; set; } = "";
	[Export] public NodePath QuestButton { get; set; } = "";
	[Export] public NodePath SaveButton { get; set; } = "";
	[Export] public NodePath MenuButton { get; set; } = "";
	[Export] public NodePath ForwardButton { get; set; } = "";
	[Export] public WorldMapDefinition? NextMap { get; set; }
	[Export] public NodePath BackButton { get; set; } = "";
	[Export(PropertyHint.File, "*.tscn")] public string PreviousScenePath { get; set; } = "";
	[Export] public NodePath HomeButton { get; set; } = "";
	[Export] public string RequiredBossId { get; set; } = "";
	[Export] public string LockedMessage { get; set; } = "";
	[Export] public bool AllowDevelopmentAccess { get; set; }
}
