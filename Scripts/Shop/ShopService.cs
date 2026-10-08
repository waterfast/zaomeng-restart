using System;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;

namespace Zaomeng.Shop;

/// <summary>交易负责钱物一同提交；界面不直接修改钱包或背包。</summary>
public sealed class ShopService
{
	public const int MaximumPurchaseCount = 9999;
	public ShopCatalog Catalog { get; }
	private readonly InventoryService _inventory;
	private readonly Wallet _wallet;

	public ShopService(ShopCatalog catalog, ItemCatalog items, InventoryService inventory, Wallet wallet)
	{
		catalog.Validate(items);
		Catalog = catalog;
		_inventory = inventory;
		_wallet = wallet;
	}

	public bool TryBuy(ShopOffer offer, int count, out string message)
	{
		if (!Catalog.Offers.Contains(offer) || count is < 1 or > MaximumPurchaseCount)
		{ message = "商品或购买数量无效。"; return false; }
		long total;
		try { total = checked(offer.UnitPrice * count); }
		catch (OverflowException) { message = "购买金额超出范围。"; return false; }
		if (!_wallet.TrySpend(offer.Currency, total))
		{ message = offer.Currency == CurrencyType.Soul ? "灵魂不足。" : "点券不足。"; return false; }
		// 先扣款，使背包通知监听者看到完整交易；入包失败退还全部货币。
		try
		{
			if (_inventory.AddItem(offer.Item.Id, count))
			{ message = $"购买成功：{offer.Item.DisplayName} × {count}"; return true; }
		}
		catch (Exception)
		{
			_wallet.Add(offer.Currency, total);
			throw;
		}
		_wallet.Add(offer.Currency, total);
		message = "物品数量已达到背包上限。";
		return false;
	}
}
