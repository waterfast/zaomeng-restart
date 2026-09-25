using Godot;

namespace Zaomeng;

public partial class HurtBox : Area2D
{
	public CharacterActor Actor { get; private set; } = null!;
	public override void _Ready() => Actor = GetParent<CharacterActor>();
}
