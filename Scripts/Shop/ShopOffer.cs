using Godot;
using Zaomeng.Items;
using Zaomeng.Save;

namespace Zaomeng.Shop;

[GlobalClass]
public partial class ShopOffer : Resource
{
	[Export] public ItemDefinition Item { get; set; } = null!;
	[Export] public CurrencyType Currency { get; set; } = CurrencyType.Soul;
	[Export] public long UnitPrice { get; set; }
}
