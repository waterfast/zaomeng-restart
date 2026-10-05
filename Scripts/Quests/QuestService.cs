using System;
using System.Linq;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using SaveCharacter = Zaomeng.Character.Character;
namespace Zaomeng.Quests;

public sealed record QuestClaimRequest(string QuestId);
public sealed record QuestClaimResult(bool Success, string Message);

/// <summary>任务条件读实时角色，已领取状态属于存档；奖励全部可存入时才一起提交。</summary>
public sealed class QuestService
{
	public QuestCatalog Catalog { get; }
	public SaveCharacter Character { get; }
	private readonly GameSaveData _save;
	private readonly InventoryService _inventory;
	private readonly ItemCatalog _items;
	public QuestService(QuestCatalog catalog, SaveCharacter character, GameSaveData save, InventoryService inventory, ItemCatalog items)
	{ catalog.Validate(items); Catalog = catalog; Character = character; _save = save; _inventory = inventory; _items = items; }
	public bool IsClaimed(QuestDefinition quest) => _save.ClaimedQuestIds.Contains(quest.Id);
	public bool CanClaim(QuestDefinition quest) => !IsClaimed(quest) && Character.Level >= quest.RequiredLevel;
	public QuestClaimResult Claim(QuestClaimRequest request)
	{
		var quest = Catalog.Definitions.FirstOrDefault(value => value.Id == request.QuestId);
		if (quest is null) return new(false, "任务未注册。");
		if (IsClaimed(quest)) return new(false, "奖励已经领取。");
		if (!CanClaim(quest)) return new(false, $"需要角色达到 {quest.RequiredLevel} 级。");
		var staged = new InventoryService(_inventory.Capacity, _items);
		staged.RestoreSlots(_inventory.Slots);
		foreach (var reward in quest.Rewards)
			if (!staged.AddItem(reward.ItemId, reward.Count)) return new(false, "背包空间不足，请整理后领取。");
		_inventory.CommitSlots(staged.Slots, () => _save.ClaimedQuestIds.Add(quest.Id));
		return new(true, "奖励已放入背包。");
	}
}

/// <summary>沿用现有同步请求和通知通道，视图只发请求；连接随地图释放。</summary>
public sealed class QuestInteraction : IDisposable
{
	public RequestChannel<QuestClaimRequest, QuestClaimResult> ClaimRequested { get; } =
		new(() => new(false, "任务服务尚未连接。"), () => new(false, "正在领取奖励。"));
	public VoidEventChannel Changed { get; } = new();
	private readonly IDisposable _connection;
	public QuestInteraction(QuestService service, Action save)
	{
		_connection = ClaimRequested.RegisterHandler(request =>
		{
			var result = service.Claim(request);
			if (result.Success)
			{
				// 奖励已原子提交，保存失败不能回滚一部分或允许再次领取。
				try { save(); }
				catch (Exception error) { result = new(true, "奖励已发放，但存档保存失败：" + error.Message); }
				Changed.Raise(this);
			}
			return result;
		});
	}
	public void Dispose() => _connection.Dispose();
}
