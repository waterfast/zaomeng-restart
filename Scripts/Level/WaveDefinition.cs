using Godot;

namespace Zaomeng.Level;

[GlobalClass]
public partial class WaveDefinition : Resource
{
	[Export] public int[] MonsterKinds { get; set; } = [];
	[Export] public float EntranceX { get; set; }
	[Export] public Vector2 SpawnRange { get; set; }
	[Export] public float GateX { get; set; }
	[Export] public int CameraRight { get; set; }
	[Export] public int LivingLimit { get; set; } = 5;
	[Export] public float SpawnInterval { get; set; } = 1.5f;
}
