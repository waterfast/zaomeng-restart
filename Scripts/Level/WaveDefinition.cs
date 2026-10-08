using Godot;

namespace Zaomeng.Level;

[GlobalClass]
public partial class WaveDefinition : Resource
{
	[Export] public Godot.Collections.Array<Zaomeng.Monsters.MonsterDefinition> Monsters { get; set; } = new();
	[Export] public float EntranceX { get; set; }
	[Export] public Vector2 SpawnRange { get; set; }
	[Export] public float GateX { get; set; }
	[Export] public int CameraRight { get; set; }
	[Export] public int LivingLimit { get; set; } = 5;
	[Export] public float SpawnInterval { get; set; } = 1.5f;
	[Export] public bool ReleaseGateOnClear { get; set; } = true;
	[Export] public float SpawnRayTop { get; set; } = 200;
	[Export] public float SpawnRayBottom { get; set; } = 650;
	[Export] public float MinimumGroundNormal { get; set; } = 0.9f;
}
