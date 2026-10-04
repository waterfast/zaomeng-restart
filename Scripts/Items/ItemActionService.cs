using System;
using System.Collections.Generic;
using System.Linq;
using Zaomeng.Equipment;
using Zaomeng.Inventory;
using Zaomeng.Save;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Items;

public sealed record ItemActionTarget(int InventorySlot, string ItemId, string InstanceId = "",
	EquipmentSlot? EquippedSlot = null);
public sealed record ItemActionRequest(string ActionId, ItemActionTarget Target);
public sealed record ItemActionOption(string Id, string Label, string UnavailableReason = "");
public sealed record ItemActionResult(bool Success, string Message = "", bool Changed = false,
	EquipmentChange? EquipmentChange = null)
{
	public static ItemActionResult Unavailable() => new(false, "物品服务尚未连接。");
	public static ItemActionResult Busy() => new(false, "正在处理物品操作，请稍后再试。");
}

public sealed record ItemActionContext(ItemActionTarget Target, ItemStack Stack,
	ItemDefinition Definition, SaveCharacter Character);

/// <summary>菜单和执行共用同一份操作规则；检查必须无副作用，执行只在请求入口发生。</summary>
public sealed record ItemActionRule(string Id, string Label,
	Func<ItemActionContext, ItemActionResult> Execute,
	Func<ItemActionContext, string>? Check = null);

/// <summary>类别默认规则可替换，单个物品可覆盖。未注册的操作不会出现在菜单，也不能被请求执行。</summary>
public sealed class ItemActionService
{
	private readonly Dictionary<ItemCategory, ItemActionRule[]> _categories = new();
	private readonly Dictionary<string, ItemActionRule[]> _items = new(StringComparer.Ordinal);
	private readonly InventoryService _inventory;
	private readonly ItemCatalog _catalog;
	private readonly SaveCharacter _character;
	private readonly Wallet _wallet;
	private readonly ItemActionRule[] _equippedActions;

	public ItemActionService(InventoryService inventory, ItemCatalog catalog, SaveCharacter character,
		Wallet wallet, EquipmentService equipment)
	{
		_inventory = inventory;
		_catalog = catalog;
		_character = character;
		_wallet = wallet;
		_equippedActions = [new("unequip", "卸下", context =>
		{
			EquipmentResult result = equipment.Execute(new(EquipmentAction.Unequip,
				ExpectedInstanceId: context.Target.InstanceId, Slot: context.Target.EquippedSlot!.Value));
			return new(result.Success, result.Message, result.Change is not null, result.Change);
		})];
		var consumables = new ConsumableService(inventory, catalog, wallet);
		RegisterCategory(ItemCategory.Consumable,
			new("use", "使用", consumables.Execute, consumables.Check),
			new("sell", "出售", Sell, CheckSell));
		RegisterCategory(ItemCategory.Material, new ItemActionRule("sell", "出售", Sell, CheckSell));
		foreach (ConsumableDefinition chest in catalog.Definitions.OfType<ConsumableDefinition>())
			if (chest.Effect == ConsumableEffect.RandomMaterialChest)
				RegisterItem(chest.Id, new("open", "打开", consumables.Execute, consumables.Check),
					new("sell", "出售", Sell, CheckSell));
		RegisterCategory(ItemCategory.Equipment,
			new("equip", "装备", context =>
			{
				EquipmentResult result = equipment.Execute(new(EquipmentAction.Equip,
					context.Target.InventorySlot, context.Target.InstanceId));
				return new(result.Success, result.Message, result.Change is not null, result.Change);
			}, CheckEquip),
			new("sell", "出售", Sell, CheckSell));
	}

	public void RegisterCategory(ItemCategory category, params ItemActionRule[] rules)
	{
		if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
		_categories[category] = ValidateRules(rules);
	}

	public void RegisterItem(string itemId, params ItemActionRule[] rules)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
		_items[itemId] = ValidateRules(rules);
	}

	private static ItemActionRule[] ValidateRules(ItemActionRule[] rules)
	{
		ArgumentNullException.ThrowIfNull(rules);
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemActionRule rule in rules)
		{
			if (rule is null || string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Label) ||
				rule.Execute is null || !ids.Add(rule.Id)) throw new ArgumentException("物品操作必须有唯一标识、名称和执行函数。");
		}
		return (ItemActionRule[])rules.Clone();
	}

	public IReadOnlyList<ItemActionOption> GetOptions(ItemActionTarget target)
	{
		if (!TryResolve(target, out ItemActionContext? context)) return Array.Empty<ItemActionOption>();
		return GetRules(context!).Select(rule => new ItemActionOption(rule.Id, rule.Label,
			rule.Check?.Invoke(context!) ?? "")).ToArray();
	}

	internal ItemActionResult Execute(ItemActionRequest request)
	{
		if (!TryResolve(request.Target, out ItemActionContext? context))
			return new(false, "该格子的物品已发生变化，请重新选择。");
		ItemActionRule? rule = GetRules(context!).FirstOrDefault(rule => rule.Id == request.ActionId);
		if (rule is null) return new(false, "该物品不支持此操作。");
		// 菜单显示时的可用状态只是提示，提交时必须基于当前状态再次检查。
		string reason = rule.Check?.Invoke(context!) ?? "";
		return reason.Length > 0 ? new(false, reason) : rule.Execute(context!);
	}

	private ItemActionRule[] GetRules(ItemActionContext context)
		=> context.Target.EquippedSlot.HasValue ? _equippedActions :
			_items.TryGetValue(context.Definition.Id, out var rules) ? rules :
			_categories.TryGetValue(context.Definition.Category, out rules) ? rules : Array.Empty<ItemActionRule>();

	private bool TryResolve(ItemActionTarget target, out ItemActionContext? context)
	{
		context = null;
		if (target.EquippedSlot is EquipmentSlot slot)
		{
			if (!Enum.IsDefined(slot)) return false;
			EquipmentInstance? instance = _character.Equipment.Get(slot);
			if (instance is null || instance.InstanceId != target.InstanceId || instance.DefinitionId != target.ItemId ||
				!_catalog.TryGetDefinition(instance.DefinitionId, out ItemDefinition? equipped)) return false;
			context = new(target, new ItemStack(instance.DefinitionId, 1, instance), equipped!, _character);
			return true;
		}
		if (target.InventorySlot < 0 || target.InventorySlot >= _inventory.Capacity) return false;
		ItemStack? stack = _inventory.Slots[target.InventorySlot];
		if (stack is null || stack.ItemId != target.ItemId ||
			(stack.Equipment?.InstanceId ?? "") != target.InstanceId ||
			!_catalog.TryGetDefinition(stack.ItemId, out ItemDefinition? definition)) return false;
		context = new(target, stack, definition!, _character);
		return true;
	}

	private static string CheckEquip(ItemActionContext context)
	{
		if (context.Definition is not EquipmentDefinition equipment || context.Stack.Equipment is null)
			return "该物品不能穿戴。";
		if (context.Character.Level < equipment.RequiredLevel) return $"需要达到 {equipment.RequiredLevel} 级。";
		return equipment.CanEquip(context.Character) ? "" : "当前角色不能穿戴这件装备。";
	}

	private string CheckSell(ItemActionContext context)
	{
		if (context.Target.EquippedSlot.HasValue || context.Definition.SellPrice <= 0)
			return "该物品不能出售。";
		// 宝石属于装备实例；先卸下再出售，避免连同宝石一起丢失。
		if (context.Stack.Equipment?.SocketedGemIds.Any(id => id.Length > 0) == true) return "请先卸下装备上的宝石。";
		return _wallet.Souls < 0 || _wallet.Souls > long.MaxValue - context.Definition.SellPrice ? "灵魂余额已达到上限。" : "";
	}

	private ItemActionResult Sell(ItemActionContext context)
	{
		var next = _inventory.Slots.ToArray();
		next[context.Target.InventorySlot] = context.Stack.Count == 1 ? null : context.Stack with { Count = context.Stack.Count - 1 };
		long balance = checked(_wallet.Souls + context.Definition.SellPrice);
		// 通知前一并更新钱包和背包，监听者不会看到只扣物品或只加钱的中间状态。
		_inventory.CommitSlots(next, () => _wallet.Souls = balance);
		return new(true, "", true);
	}
}
