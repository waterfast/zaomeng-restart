using System;
using System.Linq;
using Zaomeng.Inventory;
using Zaomeng.Save;

namespace Zaomeng.Items;

/// <summary>先暂存消耗与奖励，再一起提交；奖励放不下时不会吞掉宝箱。</summary>
public sealed class ConsumableService(InventoryService inventory, ItemCatalog catalog, Wallet wallet, Func<Player?>? activePlayer = null)
{
	public string Check(ItemActionContext context)
	{
		if (context.Definition is not ConsumableDefinition definition) return "该物品暂未实现使用效果。";
		if (definition.Effect == ConsumableEffect.ApplyBuff)
		{
			if (activePlayer?.Invoke() is not { IsDead: false } player || player.CharacterId != context.Character.Id)
				return "请在关卡内使用增益食品。";
			if (definition.Buff is null || definition.Buff.Duration <= 0) return "食品增益配置无效。";
			definition.Buff.Validate();
			return "";
		}
		if (definition.Effect == ConsumableEffect.GrantSouls)
			return definition.SoulsGranted <= 0 || wallet.Souls < 0 || wallet.Souls > long.MaxValue - definition.SoulsGranted
				? "灵魂余额已达到上限或药水配置无效。" : "";
		if (definition.Effect != ConsumableEffect.RandomMaterialChest || definition.RewardItemIds.Length == 0 ||
			definition.RewardMinCount < 1 || definition.RewardMaxCount < definition.RewardMinCount || definition.RewardMaxCount > 9999)
			return "宝箱奖励配置无效。";
		foreach (string id in definition.RewardItemIds)
			if (!catalog.TryGetDefinition(id, out ItemDefinition? reward) || reward!.Category != ItemCategory.Material)
				return "宝箱奖励尚未迁入。";
		return "";
	}

	public ItemActionResult Execute(ItemActionContext context)
	{
		string reason = Check(context);
		if (reason.Length > 0) return new(false, reason);
		var definition = (ConsumableDefinition)context.Definition;
		var next = inventory.Slots.ToArray();
		ItemStack source = next[context.Target.InventorySlot]!;
		next[context.Target.InventorySlot] = source.Count == 1 ? null : source with { Count = source.Count - 1 };
		long balance = wallet.Souls;
		string message;
		if (definition.Effect == ConsumableEffect.ApplyBuff)
		{
			activePlayer!.Invoke()!.Buffs.Apply(definition.Buff!, "consumable:" + definition.Buff!.Id);
			message = $"获得 {definition.Buff.DisplayName}。";
		}
		else if (definition.Effect == ConsumableEffect.GrantSouls)
		{
			balance = checked(balance + definition.SoulsGranted);
			message = $"获得 {definition.SoulsGranted} 灵魂。";
		}
		else
		{
			string rewardId = definition.RewardItemIds[Random.Shared.Next(definition.RewardItemIds.Length)];
			int count = Random.Shared.Next(definition.RewardMinCount, definition.RewardMaxCount + 1);
			for (int i = 0; i < count; i++)
				if (!inventory.TryAddStackable(ref next, rewardId)) return new(false, "奖励数量超出可处理范围。");
			catalog.TryGetDefinition(rewardId, out ItemDefinition? reward);
			message = $"获得 {reward!.DisplayName} × {count}。";
		}
		inventory.CommitSlots(next, () => wallet.Souls = balance);
		return new(true, message, true);
	}
}
