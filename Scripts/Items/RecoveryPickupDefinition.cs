using Godot;

namespace Zaomeng.Items;

/// <summary>拾取即恢复的关卡补给，不进入背包，数值来自独立资源。</summary>
[GlobalClass]
[Tool]
public partial class RecoveryPickupDefinition : ItemDefinition
{
	[Export] public float HealthRatio { get; set; }
	[Export] public float ManaRatio { get; set; }
	[Export] public float LifetimeSeconds { get; set; } = 6;
	public RecoveryPickupDefinition() { Category = ItemCategory.Consumable; MaxStack = 1; }
}
