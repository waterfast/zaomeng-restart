using System;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.Shop;
using Zaomeng.UI.Alchemy;
using Zaomeng.UI.Shop;

public partial class ShopAlchemySmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var items = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			var offers = GD.Load<ShopCatalog>("res://GameData/Shop/Registry.tres");
			var inventory = new InventoryService(1, items);
			var wallet = new Wallet { Souls = 1000000, Coupons = 25 };
			var service = new ShopService(offers, items, inventory, wallet);
			long[] prices = [4000, 10000, 27000, 60000];
			Check(offers.Offers.Count == 4, "四档强化石注册");
			for (int i = 0; i < 4; i++)
			{
				Check(offers.Offers[i].UnitPrice == prices[i], "旧版价格");
				Check(service.TryBuy(offers.Offers[i], 2, out _), "批量购买");
				Check(inventory.GetItemCount(offers.Offers[i].Item.Id) == 2, "全部入包");
			}
			Check(wallet.Souls == 798000 && wallet.Coupons == 25, "灵魂扣款与点券独立");
			Check(!service.TryBuy(offers.Offers[3], 9999, out _) && wallet.Souls == 798000, "余额不足不扣款");
			Check(!service.TryBuy(offers.Offers[0], 0, out _), "拒绝无效数量");
			Check(!service.TryBuy(offers.Offers[0], 10000, out _), "拒绝超额数量");
			var couponOffer = new ShopOffer { Item = offers.Offers[0].Item, Currency = CurrencyType.Coupon, UnitPrice = 5 };
			var couponCatalog = new ShopCatalog();
			couponCatalog.Offers.Add(couponOffer);
			Check(new ShopService(couponCatalog, items, inventory, wallet).TryBuy(couponOffer, 3, out _)
				&& wallet.Coupons == 10 && wallet.Souls == 798000, "点券交易");
			couponOffer.UnitPrice = long.MaxValue;
			Check(!new ShopService(couponCatalog, items, inventory, wallet).TryBuy(couponOffer, 2, out _)
				&& wallet.Coupons == 10, "金额溢出不扣款");
			couponCatalog.Dispose();
			couponOffer.Dispose();
			var data = InventorySaveMapper.Capture(inventory);
			data.Wallet = wallet;
			var serializer = new JsonSaveSerializer();
			var restored = serializer.Deserialize(serializer.Serialize(data));
			Check(restored.Wallet.Coupons == 10 && restored.Wallet.Souls == 798000, "双货币存档往返");
			var restoredInventory = new InventoryService(restored.Inventory.Capacity, items);
			InventorySaveMapper.Restore(restored, restoredInventory);
			Check(restoredInventory.GetItemCount("qhs_1") == 5, "购买物品存档恢复");
			var shop = GD.Load<PackedScene>("res://Scenes/UI/Shop/SHOP.tscn").Instantiate<Node2D>();
			AddChild(shop);
			var panel = new LegacyShopPanel();
			shop.AddChild(panel);
			int saves = 0;
			panel.Bind(shop, service, wallet, "悟空", () => saves++);
			var card = shop.GetNode<Node2D>("BG/BG2/Shop_item");
			card.GetNode<LineEdit>("BG/num_/num_kj/num_").Text = "3";
			card.GetNode<Button>("BG/goumai").EmitSignal(BaseButton.SignalName.Pressed);
			Check(saves == 1 && inventory.GetItemCount("qhs_1") == 8, "UI 批量购买和保存");
			Check(shop.GetNode<Label>("BG/Balances/Coupons").Text == "点券：10", "点券余额显示");
			bool closed = false;
			panel.CloseRequested += () => closed = true;
			shop.GetNode<Button>("BG/Close").EmitSignal(BaseButton.SignalName.Pressed);
			Check(closed, "商店关闭");
			shop.GetNode<LineEdit>("BG/BG2/HBoxContainer2/LineEdit").Text = "不存在";
			panel.Refresh();
			Check(shop.GetNode("BG/BG2").GetNodeOrNull("Shop_item") is null, "空搜索结果");
			shop.GetNode<LineEdit>("BG/BG2/HBoxContainer2/LineEdit").Text = "";
			panel.Refresh();
			if (Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				DirAccess.MakeDirRecursiveAbsolute("res://.godot/shop-alchemy-checks");
				foreach (Node node in shop.GetChildren()) if (node is AcceptDialog dialog) dialog.Hide();
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				SaveCapture("shop");
			}
			shop.Hide();
			var alchemy = GD.Load<PackedScene>("res://Scenes/UI/LDL/ldl.tscn").Instantiate<Node2D>();
			AddChild(alchemy);
			var furnace = new LegacyAlchemyPanel();
			alchemy.AddChild(furnace);
			furnace.Bind(alchemy, inventory, items, wallet);
			foreach (string tab in new[] { "hc", "qh", "xq", "hh", "fj", "dz" })
			{
				alchemy.GetNode<Button>(tab).EmitSignal(BaseButton.SignalName.Pressed);
				Check(furnace.CurrentFunction.Length > 0, "炼丹炉功能切换");
				if (Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture"))
				{
					await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
					SaveCapture($"alchemy-{tab}");
				}
			}
			furnace.SelectFunction("hc");
			alchemy.GetNode<TextureButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/dj").EmitSignal(BaseButton.SignalName.Pressed);
			Check(alchemy.GetNode<Label>("Main_Backpack/MarginContainer/VBoxContainer/title").Text == "道具背包", "炼丹炉背包分类");
			Check(inventory.GetItemCount("qhs_1") == 8 && wallet.Souls == 786000, "切换不消耗材料和货币");
			if (Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				SaveCapture("alchemy");
			}
			alchemy.QueueFree();
			shop.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			// 注入仅内存的会话，地图验证不读取或写入玩家存档。
			GameSession.SelectNewSlot(0);
			data.Characters.Add(new Zaomeng.Character.Character { Id = "role_1", Name = "悟空" });
			typeof(GameSession).GetProperty(nameof(GameSession.Data))!.SetValue(null, data);
			typeof(GameSession).GetProperty(nameof(GameSession.Inventory))!.SetValue(null, inventory);
			foreach (var entry in new[] { (1, "Shop", "ldl"), (2, "shop", "LdL"), (3, "Shop", "ldl") })
			{
				var map = GD.Load<PackedScene>($"res://Scenes/UI/MainMenu/Map{entry.Item1}.tscn").Instantiate<Node2D>();
				AddChild(map);
				Check(!map.GetNode<TextureButton>(entry.Item2).Disabled && !map.GetNode<TextureButton>(entry.Item3).Disabled, "地图入口启用");
				map.GetNode<TextureButton>(entry.Item2).EmitSignal(BaseButton.SignalName.Pressed);
				Check(GetTree().Paused, "打开商店暂停");
				map.GetNode<TextureButton>(entry.Item3).EmitSignal(BaseButton.SignalName.Pressed);
				map.QueueFree();
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				Check(!GetTree().Paused, "离开地图恢复暂停状态");
			}
			GD.Print("SHOP_ALCHEMY_SMOKE_PASS");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void Check(bool condition, string message)
	{ if (!condition) throw new InvalidOperationException(message); }
	private void SaveCapture(string name)
	{
		using Image frame = GetViewport().GetTexture().GetImage();
		frame.SavePng($"res://.godot/shop-alchemy-checks/{name}.png");
	}
}
