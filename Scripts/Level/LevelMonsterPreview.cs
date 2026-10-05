using Godot;

namespace Zaomeng.Level;

[GlobalClass]
public partial class LevelMonsterPreview : Resource
{
	[Export] public string DisplayName { get; set; } = "";
	[Export] public Texture2D? Icon { get; set; }
	[Export] public bool IsBoss { get; set; }
}
