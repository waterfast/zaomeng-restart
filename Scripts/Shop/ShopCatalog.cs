using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Items;

namespace Zaomeng.Shop;

[GlobalClass]
public partial class ShopCatalog : Resource
{
	[Export] public Godot.Collections.Array<ShopOffer> Offers { get; set; } = new();

	public void Validate(ItemCatalog items)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (ShopOffer offer in Offers)
			if (offer?.Item is null || offer.UnitPrice <= 0 || !Enum.IsDefined(offer.Currency) ||
				!items.TryGetDefinition(offer.Item.Id, out ItemDefinition? registered) || registered != offer.Item ||
				!ids.Add(offer.Item.Id))
				throw new InvalidOperationException("商店商品重复，或物品、价格、货币配置无效。");
	}
}
