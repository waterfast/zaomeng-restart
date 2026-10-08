using Godot;
namespace Zaomeng.Audio;
[GlobalClass]
public partial class MusicTrack : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public AudioStream Stream { get; set; } = null!;
}
