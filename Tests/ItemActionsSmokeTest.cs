using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.UI.Inventory;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

public partial class ItemActionsSmokeTest : Node
{
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			CheckRules();
			CheckConsumables();
			await CheckMenu();
			GD.Print("ITEM_ACTIONS_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}

	private static void CheckRules()
	{
		var sword = new EquipmentDefinition { Id = "sword", SellPrice = 25, GemSocketCount = 1 };
		var gem = new GemDefinition { Id = "gem", Bonus = 1 };
		var potion = new ItemDefinition { Id = "potion", Category = ItemCategory.Consumable, MaxStack = 9 };
		var chest = new ItemDefinition { Id = "chest", Category = ItemCategory.Consumable };
		var catalog = new ItemCatalog { Definitions = [sword, gem, potion, chest] };
		var inventory = new InventoryService(4, catalog);
		inventory.AddItem("sword", 2);
		inventory.AddItem("potion", 1);
		inventory.AddItem("chest", 1);
		var wallet = new Wallet { Souls = 10 };
		var character = new SaveCharacter { Id = "role_1" };
		var events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		int saves = 0, changes = 0;
		byte[] saved = [];
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events, () => { }, () =>
		{
			saves++;
			saved = new JsonSaveSerializer().Serialize(InventorySaveMapper.Capture(inventory,
				new GameSaveData { Wallet = wallet, Characters = [character] }));
		}, wallet);
		ItemActionTarget first = new(0, "sword", inventory.Slots[0]!.Equipment!.InstanceId);
		ItemActionTarget second = new(1, "sword", inventory.Slots[1]!.Equipment!.InstanceId);
		using var completed = events.ItemActionCompleted.Subscribe(() => changes++);
		Check(events.ItemActionRequested.GetOptions(first).Select(option => option.Id).SequenceEqual(["equip", "sell"]),
			"equipment exposes equip and sell from the service");
		Check(!events.ItemActionRequested.Send(new("sell", first with { InstanceId = second.InstanceId })).Success,
			"same-name instance replacement cannot sell the wrong equipment");
		Check(!events.ItemActionRequested.Send(new("open", first)).Success && saves == 0 && changes == 0,
			"unsupported actions have no side effects");
		wallet.Souls = long.MaxValue;
		Check(!events.ItemActionRequested.Send(new("sell", first)).Success && inventory.Slots[0] is not null,
			"wallet overflow fails before removing equipment");
		wallet.Souls = 10;
		sword.SellPrice = 0;
		Check(!events.ItemActionRequested.Send(new("sell", first)).Success, "zero-price equipment cannot be sold");
		sword.SellPrice = 25;
		bool atomic = false, nestedRejected = false;
		void Observe()
		{
			atomic = inventory.Slots[0] is null && wallet.Souls == 35;
			nestedRejected = !events.ItemActionRequested.Send(new("sell", second)).Success &&
				!events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Success;
		}
		inventory.Changed += Observe;
		Check(events.ItemActionRequested.Send(new("sell", first)).Success && atomic && nestedRejected,
			"sale commits wallet and inventory together and blocks nested operations");
		inventory.Changed -= Observe;
		Check(wallet.Souls == 35 && inventory.Slots[1]!.Equipment!.InstanceId == second.InstanceId && saves == 1 && changes == 1,
			"sale removes only the chosen instance and saves and notifies once");
		GameSaveData restored = new JsonSaveSerializer().Deserialize(saved);
		Check(restored.Wallet.Souls == 35 && restored.Inventory.Slots[0] is null &&
			restored.Inventory.Slots[1]!.Equipment!.InstanceId == second.InstanceId,
			"saved state retains sale income and the unsold equipment identity");
		Check(!events.ItemActionRequested.Send(new("sell", first)).Success && wallet.Souls == 35 && saves == 1,
			"repeated sale cannot award money twice");
		inventory.AddItem("gem", 1);
		Check(events.GemRequested.Send(new(GemAction.Socket, second.InstanceId, 0, 0, "gem")).Success,
			"socket fixture uses the real gem request");
		Check(!events.ItemActionRequested.Send(new("sell", second)).Success && inventory.Slots[1] is not null,
			"socketed gems must be removed before sale");
		bool canUse = true;
		int used = 0, opened = 0;
		binding.ItemActions.RegisterCategory(ItemCategory.Consumable,
			new ItemActionRule("use", "使用", _ => { used++; return new(true); }, _ => canUse ? "" : "当前不能使用。"));
		binding.ItemActions.RegisterItem("chest", new ItemActionRule("open", "打开", _ => { opened++; return new(true); }));
		ItemActionTarget potionTarget = new(2, "potion");
		Check(events.ItemActionRequested.GetOptions(potionTarget).Single().Id == "use" &&
			events.ItemActionRequested.GetOptions(new(3, "chest")).Single().Id == "open",
			"category defaults and per-item overrides support use and chest actions");
		canUse = false;
		Check(events.ItemActionRequested.GetOptions(potionTarget).Single().UnavailableReason.Length > 0 &&
			!events.ItemActionRequested.Send(new("use", potionTarget)).Success && used == 0,
			"execution rechecks usability even after a menu was shown");
		canUse = true;
		Check(events.ItemActionRequested.Send(new("use", potionTarget)).Success && used == 1 &&
			events.ItemActionRequested.Send(new("open", new(3, "chest"))).Success && opened == 1,
			"custom actions dispatch to their own handlers");
	}

	private static void CheckConsumables()
	{
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		catalog.Validate();
		var events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		var inventory = new InventoryService(1, catalog);
		var wallet = new Wallet { Souls = 7 };
		var character = new SaveCharacter { Id = "role_1" };
		int saves = 0;
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
			() => { }, () => saves++, wallet);
		inventory.AddItem("qhsbx", 2);
		var chest = new ItemActionTarget(0, "qhsbx");
		Check(events.ItemActionRequested.GetOptions(chest).Select(option => option.Id).SequenceEqual(["open", "sell"]),
			"migrated chest exposes open and sell");
		Check(events.ItemActionRequested.Send(new("open", chest)).Success && inventory.GetItemCount("qhsbx") == 1 && saves == 1
			&& inventory.Capacity > 1, "opening a chest grows the bag to hold all rewards");
		Check(events.ItemActionRequested.Send(new("sell", chest)).Success && inventory.GetItemCount("qhsbx") == 0 && wallet.Souls == 207,
			"selling a stackable chest consumes exactly one and uses the original price");
		inventory.AddItem("qhsbx", 1);
		Check(events.ItemActionRequested.Send(new("open", chest)).Success && inventory.GetItemCount("qhsbx") == 0 && saves == 3
			&& inventory.Slots.Where(s => s?.ItemId.StartsWith("qhs_") == true).Sum(s => s!.Count) is >= 6 and <= 8,
			"each chest grants 3 to 4 original strengthening stones without losing earlier rewards");
		inventory.RestoreSlots([null]);
		inventory.AddItem("xlhys", 2);
		var potion = new ItemActionTarget(0, "xlhys");
		Check(events.ItemActionRequested.GetOptions(potion).Single(option => option.Id == "sell").UnavailableReason.Length > 0,
			"zero-price soul potions expose a disabled sell option");
		Check(events.ItemActionRequested.Send(new("use", potion)).Success && wallet.Souls == 150207 && inventory.GetItemCount("xlhys") == 1,
			"small soul potion grants 150000 to the same save wallet and consumes one");
		wallet.Souls = long.MaxValue;
		int before = saves;
		Check(!events.ItemActionRequested.Send(new("use", potion)).Success && inventory.GetItemCount("xlhys") == 1 && saves == before,
			"overflow does not consume the potion or save");
		wallet.Souls = 0;
		inventory.RestoreSlots([null]);
		inventory.AddItem("dlhys", 1);
		Check(events.ItemActionRequested.Send(new("use", new(0, "dlhys"))).Success && wallet.Souls == 600000 && inventory.Slots[0] is null,
			"large soul potion grants the original 600000 soul reward");
	}

	private async Task CheckMenu()
	{
		GetViewport().GuiEmbedSubwindows = true;
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		var events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		var inventory = new InventoryService(6, catalog);
		inventory.AddItem("ryjgb", 2);
		inventory.AddItem("qhsbx", 2);
		inventory.AddItem("xlhys", 2);
		inventory.AddItem("dlhys", 2);
		var character = new SaveCharacter { Id = "role_1" };
		var wallet = new Wallet();
		var player = new Player();
		player.BindCharacter(character, catalog);
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
			player.RefreshCharacterStats, () => { }, wallet);
		Node2D backpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackPack.tscn").Instantiate<Node2D>();
		backpack.ProcessMode = ProcessModeEnum.Always;
		AddChild(backpack);
		using var adapter = new InventoryViewAdapter(inventory, catalog);
		using (var view = new LegacyBackpackView(backpack, adapter, catalog, character, wallet, player, () => { }, events))
		{
			GetTree().Paused = true;
			// 等主窗口取得焦点、容器完成布局后再打开弹出框，避免启动焦点事件关闭它。
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Button slot = backpack.GetNode<GridContainer>(
				"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box").GetChild<Button>(0);
			slot.EmitSignal(BaseButton.SignalName.Pressed);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			PopupPanel menu = backpack.GetNode<PopupPanel>("ItemActionMenu");
			Check(menu.Visible && character.Equipment.Get(EquipmentSlot.Weapon) is null,
				"single click opens the legacy action menu without equipping immediately, during pause");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/item-actions-preview.png");
			}
			menu.GetNode<Button>("Options/VBoxContainer/sell").EmitSignal(BaseButton.SignalName.Pressed);
			Check(inventory.Slots[0] is null && wallet.Souls > 0 && !menu.Visible &&
				backpack.GetNode<Label>("Main_Backpack/coin_text/coin_number").Text == wallet.Souls.ToString(),
				"actual sell button updates inventory and currency label and closes the menu");
			slot.EmitSignal(BaseButton.SignalName.Pressed);
			inventory.MoveStack(1, 0, 1);
			Check(!menu.Visible, "inventory movement invalidates an open menu");
			slot.EmitSignal(BaseButton.SignalName.Pressed);
			menu.GetNode<Button>("Options/VBoxContainer/equip").EmitSignal(BaseButton.SignalName.Pressed);
			Button worn = backpack.GetNode<Button>("background/infomation/equ_/HBoxContainer/wq");
			worn.EmitSignal(BaseButton.SignalName.MouseEntered);
			Check(worn.Icon == slot.Icon && worn.TooltipText.Length == 0 && backpack.GetNode<Node2D>("ItemTooltip").Visible &&
				backpack.GetNode<Label>("ItemTooltip/pro_wk/information/inf/RegisteredStats/attack").Text.Contains("75"),
				"worn equipment has a grid background and the same detailed tooltip as inventory gear");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/worn-equipment-preview.png");
			}
			worn.EmitSignal(BaseButton.SignalName.Pressed);
			Check(character.Equipment.Get(EquipmentSlot.Weapon) is not null &&
				menu.GetNode<VBoxContainer>("Options/VBoxContainer").GetChildren().OfType<Button>().Single().Name == "unequip",
				"clicking worn equipment shows only unequip without immediately removing it");
			EquipmentInstance selected = character.Equipment.Get(EquipmentSlot.Weapon)!;
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/unequip-menu-preview.png");
			}
			menu.GetNode<Button>("Options/VBoxContainer/unequip").EmitSignal(BaseButton.SignalName.Pressed);
			Check(character.Equipment.Get(EquipmentSlot.Weapon) is null && inventory.Slots[0]!.Equipment!.InstanceId == selected.InstanceId,
				"actual unequip option returns the same individual to the bag");
			Check(!events.ItemActionRequested.Send(new("unequip", new(-1, selected.DefinitionId, selected.InstanceId, EquipmentSlot.Weapon))).Success,
				"a stale worn-equipment menu cannot remove a replacement");
			backpack.GetNode<TextureButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/xhp").EmitSignal(BaseButton.SignalName.Pressed);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			slot.EmitSignal(BaseButton.SignalName.Pressed);
			Check(menu.Visible && menu.GetNodeOrNull<Button>("Options/VBoxContainer/open") is not null && menu.GetNodeOrNull<Button>("Options/VBoxContainer/sell") is not null,
				"actual consumables tab opens the migrated chest actions");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/consumables-preview.png");
			}
			menu.GetNode<Button>("Options/VBoxContainer/open").EmitSignal(BaseButton.SignalName.Pressed);
			Check(inventory.GetItemCount("qhsbx") == 1 && inventory.Slots.Any(stack => stack?.ItemId.StartsWith("qhs_") == true),
				"actual open button consumes one box and puts its reward in the bag");
			backpack.GetNode<AcceptDialog>("EquipmentFeedback").Hide();
			slot.EmitSignal(BaseButton.SignalName.Pressed);
			backpack.Hide();
			Check(!menu.Visible, "closing backpack also closes item actions");
			GetTree().Paused = false;
		}
		Check(events.ItemActionCompleted.ListenerCount == 0, "view disposal removes item completion observer");
		RemoveChild(backpack);
		backpack.QueueFree();
		player.Free();
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"  PASS: {message}");
	}
}
