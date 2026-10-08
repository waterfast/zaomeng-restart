using Godot;
namespace Zaomeng.Monsters;
[GlobalClass]
public partial class MonsterDropEntry : Resource
{
	[Export] public string ItemId { get; set; } = "";
	[Export] public float Probability { get; set; } = 1;
	[Export] public int MinimumCount { get; set; } = 1;
	[Export] public int MaximumCount { get; set; } = 1;
}
