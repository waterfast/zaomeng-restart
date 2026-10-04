using System;
using Godot;

namespace Zaomeng.Events;

/// <summary>关卡和界面引用同一组事件资源；不进入 GameSaveData，不存储玩家状态。</summary>
[GlobalClass]
public partial class GameplayEvents : Resource
{
	[Export] public EquipmentRequestChannel EquipmentRequested { get; set; } = null!;
	[Export] public EquipmentChangedChannel EquipmentChanged { get; set; } = null!;
	[Export] public VoidEventChannel InventoryChanged { get; set; } = null!;
	[Export] public StringEventChannel SaveFailed { get; set; } = null!;
	[Export] public GemRequestChannel GemRequested { get; set; } = null!;
	[Export] public EquipmentModifiedChannel EquipmentModified { get; set; } = null!;
	[Export] public ItemActionRequestChannel ItemActionRequested { get; set; } = null!;
	[Export] public VoidEventChannel ItemActionCompleted { get; set; } = null!;

	public void Validate()
	{
		if (EquipmentRequested is null || EquipmentChanged is null || InventoryChanged is null || SaveFailed is null ||
			GemRequested is null || EquipmentModified is null || ItemActionRequested is null || ItemActionCompleted is null)
			throw new InvalidOperationException("GameplayEvents 缺少事件资源，请在检查器中补齐。");
	}
}
