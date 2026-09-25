using Godot;

namespace Zaomeng;

[GlobalClass]
public partial class HitDefinition : Resource
{
	[Export] public float Damage { get; set; } = 12;
	[Export] public Vector2 Knockback { get; set; } = new(60, 0);
	[Export] public float Hitstun { get; set; } = 0.22f;
}
