using Godot;
namespace Zaomeng.Quests;

[GlobalClass]
public partial class QuestDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string NameKey { get; set; } = "";
	[Export] public string DescriptionKey { get; set; } = "";
	[Export] public int RequiredLevel { get; set; } = 1;
	[Export] public Godot.Collections.Array<QuestReward> Rewards { get; set; } = new();
	public string DisplayName => TranslationServer.Translate(NameKey).ToString();
	public string Description => TranslationServer.Translate(DescriptionKey).ToString();
}
