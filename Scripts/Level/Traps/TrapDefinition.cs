using Godot;

namespace Zaomeng.Level.Traps;

[GlobalClass]
[Tool]
public partial class TrapDefinition : Resource
{
	[Export] public float Damage { get; set; } = 50;
	[Export] public float RestSeconds { get; set; } = 1.4f;
	[Export] public float WarningSeconds { get; set; } = 0.4f;
	[Export] public float ActiveSeconds { get; set; } = 0.6f;
	public void Validate()
	{
		if (!float.IsFinite(Damage) || Damage < 0 || !float.IsFinite(RestSeconds) || RestSeconds <= 0
			|| !float.IsFinite(WarningSeconds) || WarningSeconds <= 0 || !float.IsFinite(ActiveSeconds) || ActiveSeconds <= 0)
			throw new System.InvalidOperationException("陷阱伤害或周期配置无效。");
	}
}
