using Godot;
namespace Zaomeng.Audio;
[GlobalClass]
public partial class AudioCatalog : Resource
{
	[Export] public Godot.Collections.Array<MusicTrack> Tracks { get; set; } = new();
	[Export] public MusicTrack MenuDefault { get; set; } = null!;
	[Export] public AudioStream? Click { get; set; }
	[Export] public AudioStream? Pickup { get; set; }
	[Export] public AudioStream? Victory { get; set; }
	[Export] public AudioStream? Defeat { get; set; }
	public MusicTrack? Find(string id)
	{
		foreach (var track in Tracks) if (track.Id == id) return track;
		return null;
	}
}
