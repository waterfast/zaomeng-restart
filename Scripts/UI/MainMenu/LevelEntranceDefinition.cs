using Godot;
using Zaomeng.Level;

namespace Zaomeng.UI.MainMenu;

[GlobalClass]
public partial class LevelEntranceDefinition : Resource
{
	[Export] public NodePath ButtonPath { get; set; } = "";
	[Export] public Godot.Collections.Array<LevelDefinition> Levels { get; set; } = new();
}
