using Godot;
namespace Zaomeng.Quests;

[GlobalClass]
public partial class QuestReward : Resource
{
	[Export] public string ItemId { get; set; } = "";
	[Export] public int Count { get; set; } = 1;
}
