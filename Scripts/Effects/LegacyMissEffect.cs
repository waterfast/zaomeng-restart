using Godot;

namespace Zaomeng;

public partial class LegacyMissEffect : Node2D
{
	public override void _Ready() => GetNode<AnimationPlayer>("AnimationPlayer").Play("miss");
}
