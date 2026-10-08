using Godot;

namespace Zaomeng.Combat.Effects;

[GlobalClass]
[Tool]
public abstract partial class PassiveCondition : Resource
{
	public abstract bool Matches(CharacterActor owner);
	public abstract void Validate();
}
