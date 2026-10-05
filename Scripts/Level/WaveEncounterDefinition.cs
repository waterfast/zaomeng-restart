using Godot;

namespace Zaomeng.Level;

/// <summary>只描述波次关卡的出怪和门禁；非波次关卡无需此资源。</summary>
[GlobalClass]
public partial class WaveEncounterDefinition : Resource
{
	[Export] public Godot.Collections.Array<WaveDefinition> Waves { get; set; } = new();
	[Export] public float FinalBoundaryX { get; set; } = 4000;
	[Export] public Vector2 ExitPosition { get; set; } = new(3880, 427);
}
