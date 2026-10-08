using Godot;
namespace Zaomeng.Audio;
[GlobalClass]
public partial class ActorSoundProfile : Resource
{
	[Export] public AudioStream? Impact { get; set; }
	[Export] public AudioStream? Hurt { get; set; }
	[Export] public AudioStream? Death { get; set; }
}
