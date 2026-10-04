using System;
using Godot;
using Zaomeng.Character;
using Zaomeng.Equipment;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.UI.Inventory;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

/// <summary>使用独立角色与内存存档，验证事件资源、真实交换和场景生命周期。</summary>
public partial class EquipmentEventsSmokeTest : Node
{
	public override void _Ready()
	{
		try
		{
			CheckRuntimeChannels();
			CheckEquipment();
			CheckListenerLifecycle();
			CheckBackpackInteraction();
			CheckSaveFailure();
			GD.Print("EQUIPMENT_EVENTS_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"EQUIPMENT_EVENTS_TEST: FAIL: {error}");
			GetTree().Quit(1);
		}
	}

	private static void CheckRuntimeChannels()
	{
		int errors = 0, calls = 0;
		var notifications = new NotificationChannel<int>(_ => errors++);
		using var broken = notifications.Subscribe(_ => throw new InvalidOperationException("测试监听异常"));
		using var healthy = notifications.Subscribe(value => calls += value);
		notifications.Raise(2);
		Check(errors == 1 && calls == 2, "one failing observer does not block others");
		healthy.Dispose();
		healthy.Dispose();
		Check(notifications.ListenerCount == 1, "subscription disposal is independent and idempotent");

		var removable = new NotificationChannel<int>(_ => errors++);
		IDisposable? second = null;
		using var first = removable.Subscribe(_ => second!.Dispose());
		second = removable.Subscribe(_ => calls++);
		removable.Raise(1);
		Check(calls == 2, "disposed observer is skipped during an active dispatch");

		var requests = new RequestChannel<int, int>(() => -1, () => -2);
		Check(requests.Send(0) == -1, "unbound request returns unavailable");
		using (requests.RegisterHandler(value => requests.Send(value)))
		{
			Check(requests.Send(0) == -2, "nested request is rejected");
			bool rejected = false;
			try { requests.RegisterHandler(value => value); }
			catch (InvalidOperationException) { rejected = true; }
			Check(rejected, "a request cannot acquire a second handler");
		}
		Check(requests.Send(0) == -1, "handler disconnect restores unavailable result");
		using (requests.RegisterHandler(_ => throw new InvalidOperationException()))
		{
			try { requests.Send(0); }
			catch (InvalidOperationException) { }
		}
		using var next = requests.RegisterHandler(value => value + 1);
		Check(requests.Send(4) == 5, "throwing handler does not leave channel busy");
	}

	private static void CheckEquipment()
	{
		GameplayEvents events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		events.Validate();
		Check(events.EquipmentRequested.ListenerCount == 0, "authored request resource has no saved runtime handler");
		var first = new EquipmentDefinition { Id = "first", Attack = 10 };
		var second = new EquipmentDefinition { Id = "second", Attack = 20 };
		var locked = new EquipmentDefinition { Id = "locked", RequiredLevel = 5 };
		var restricted = new EquipmentDefinition { Id = "restricted", RequiredCharacterId = "role_2" };
		var material = new ItemDefinition { Id = "material", Category = ItemCategory.Material };
		var catalog = new ItemCatalog { Definitions = [first, second, locked, restricted, material] };
		catalog.Validate();
		var character = new SaveCharacter { Id = "role_1", BaseStats = new() { Attack = 3 } };
		var inventory = new InventoryService(1, catalog);
		character.Equipment.Set(EquipmentSlot.Weapon, EquipmentInstance.Create(first.Id, 0));
		inventory.AddItem(second.Id, 1);
		int bagChanges = 0, equipmentChanges = 0, saves = 0;
		bool bagConsistent = false, equipmentConsistent = false, nestedRejected = false;
		float attack = 13;
		byte[] json = [];
		using var bagSubscription = events.InventoryChanged.Subscribe(() =>
		{
			bagChanges++;
			bagConsistent = character.Equipment.Get(EquipmentSlot.Weapon)?.DefinitionId == second.Id && inventory.Slots[0]?.ItemId == first.Id && attack == 23;
		});
		using var equipmentSubscription = events.EquipmentChanged.Subscribe(change =>
		{
			equipmentChanges++;
			equipmentConsistent = change.CharacterId == "role_1" && change.Previous?.DefinitionId == first.Id &&
				change.Current?.DefinitionId == second.Id && events.EquipmentChanged.LastSender.Length > 0;
			nestedRejected = events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Error == EquipmentError.Busy;
		});
		using (var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
			() => attack = CharacterStatCalculator.Calculate(character, catalog).Attack,
			() =>
			{
				Check(attack == 23, "save runs after character attributes are applied");
				json = new JsonSaveSerializer().Serialize(InventorySaveMapper.Capture(inventory,
					new GameSaveData { Characters = [character] }));
				saves++;
			}, new Wallet()))
		{
			string incomingInstanceId = inventory.Slots[0]!.Equipment!.InstanceId;
			EquipmentResult result = events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, incomingInstanceId), "测试界面");
			Check(result.Success && bagChanges == 1 && equipmentChanges == 1 && saves == 1,
				"full inventory swaps using the slot freed by incoming equipment");
			Check(bagConsistent && equipmentConsistent && nestedRejected,
				"observers see committed states, current sender, updated stats, and reject nested requests");
			GameSaveData saved = new JsonSaveSerializer().Deserialize(json);
			Check(saved.Characters[0].Equipment.Get(EquipmentSlot.Weapon)?.DefinitionId == second.Id && saved.Inventory.Slots[0]?.ItemId == first.Id,
				"save captures both sides of the swap without losing items");
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Error == EquipmentError.InventoryFull,
				"unequip into a full inventory fails");
			Check(character.Equipment.Get(EquipmentSlot.Weapon)?.DefinitionId == second.Id && inventory.Slots[0]?.ItemId == first.Id && saves == 1,
				"failed unequip preserves both states and does not save");
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, incomingInstanceId)).Error == EquipmentError.ItemChanged,
				"stale slot identity is rejected");
			Check(bagChanges == 1 && equipmentChanges == 1, "failed requests publish no state notifications");
		}
		Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Error == EquipmentError.ServiceUnavailable,
			"leaving gameplay releases the resource handler");
		bagSubscription.Dispose();
		equipmentSubscription.Dispose();
		using (var nextBinding = new GameplayEquipmentBinding(character, catalog, inventory, events,
			() => attack = CharacterStatCalculator.Calculate(character, catalog).Attack, () => saves++, new Wallet()))
		{
			inventory.RestoreSlots([null]);
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Success && attack == 3 && saves == 2,
				"next gameplay can bind and successfully unequip");
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Success && saves == 2,
				"empty unequip does not save twice");
		}
		Check(events.InventoryChanged.ListenerCount == 0 && events.EquipmentChanged.ListenerCount == 0,
			"disposed UI connections do not leak into next gameplay");

		var service = new EquipmentService(character, catalog, inventory);
		inventory.RestoreSlots([new(locked.Id, 1, EquipmentInstance.Create(locked.Id, 0))]);
		Check(service.Execute(new(EquipmentAction.Equip, 0, inventory.Slots[0]!.Equipment!.InstanceId)).Error == EquipmentError.LevelTooLow,
			"level restriction is enforced");
		inventory.RestoreSlots([new(restricted.Id, 1, EquipmentInstance.Create(restricted.Id, 0))]);
		Check(service.Execute(new(EquipmentAction.Equip, 0, inventory.Slots[0]!.Equipment!.InstanceId)).Error == EquipmentError.WrongCharacter,
			"character restriction is enforced");
		inventory.RestoreSlots([new(material.Id, 1)]);
		Check(service.Execute(new(EquipmentAction.Equip, 0, material.Id)).Error == EquipmentError.NotEquipment,
			"materials cannot be equipped");
		Check(service.Execute(new(EquipmentAction.Equip, -1)).Error == EquipmentError.InvalidSlot,
			"invalid slot is rejected without throwing");
	}

	private void CheckListenerLifecycle()
	{
		var channel = new IntEventChannel();
		var listener = new IntEventListener { EventChannel = channel };
		int received = 0;
		listener.Received += value => received += value;
		AddChild(listener);
		channel.Raise(2, this);
		Check(received == 2 && channel.ListenerCount == 1, "listener node converts resource notifications to Godot signals");
		Check(channel.Get("Runtime/ListenerCount").AsInt32() == 1 && channel.Get("Runtime/RaiseCount").AsInt32() == 1,
			"read-only inspector fields expose runtime connection and raise counts");
		RemoveChild(listener);
		channel.Raise(4, this);
		Check(received == 2 && channel.ListenerCount == 0, "exiting scene tree disconnects listener node");
		AddChild(listener);
		channel.Raise(3, this);
		Check(received == 5 && channel.ListenerCount == 1, "reentering scene tree reconnects exactly once");
		RemoveChild(listener);
		listener.Free();
	}

	private void CheckBackpackInteraction()
	{
		GameplayEvents events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		ItemCatalog catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		var character = new SaveCharacter { Id = "role_1", BaseStats = new() { MaxHealth = 80, Attack = 8 } };
		var inventory = new InventoryService(2, catalog);
		inventory.AddItem("ryjgb", 1);
		var player = new Player();
		player.BindCharacter(character, catalog);
		int saves = 0;
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
			player.RefreshCharacterStats, () => saves++, new Wallet());
		Node2D backpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackPack.tscn").Instantiate<Node2D>();
		AddChild(backpack);
		using var adapter = new InventoryViewAdapter(inventory, catalog);
		using (var view = new LegacyBackpackView(backpack, adapter, catalog, character, new Wallet(), player,
			() => saves++, events))
		{
			Button itemButton = backpack.GetNode<GridContainer>(
				"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box").GetChild<Button>(0);
			itemButton.EmitSignal(BaseButton.SignalName.Pressed);
			backpack.GetNode<Button>("ItemActionMenu/Options/VBoxContainer/equip").EmitSignal(BaseButton.SignalName.Pressed);
			Check(character.Equipment.Get(EquipmentSlot.Weapon)?.DefinitionId == "ryjgb" && player.Attack == 83 && saves == 1,
				"actual backpack action menu equips and refreshes player");
			Button weaponButton = backpack.GetNode<Button>("background/infomation/equ_/HBoxContainer/wq");
			Check(weaponButton.GetNode<TextureRect>("ItemIcon").Texture is not null && weaponButton.TooltipText == "",
				"equipped grid icon refreshes without a system tooltip");
			weaponButton.EmitSignal(BaseButton.SignalName.Pressed);
			backpack.GetNode<Button>("ItemActionMenu/Options/VBoxContainer/unequip").EmitSignal(BaseButton.SignalName.Pressed);
			Check(character.Equipment.Get(EquipmentSlot.Weapon) is null && player.Attack == 8 && inventory.GetItemCount("ryjgb") == 1 && saves == 2,
				"actual equipment slot click returns item to backpack");
		}
		Check(events.EquipmentChanged.ListenerCount == 0 && events.SaveFailed.ListenerCount == 0,
			"backpack disposal releases its resource subscriptions");
		RemoveChild(backpack);
		backpack.QueueFree();
		player.Free();
	}

	private static void CheckSaveFailure()
	{
		GameplayEvents events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		var weapon = new EquipmentDefinition { Id = "save_probe", Attack = 10 };
		var catalog = new ItemCatalog { Definitions = [weapon] };
		var character = new SaveCharacter { Id = "role_1" };
		var inventory = new InventoryService(1, catalog);
		inventory.AddItem(weapon.Id, 1);
		int saveFailures = 0, changes = 0;
		using var failureSubscription = events.SaveFailed.Subscribe(_ => saveFailures++);
		using var changeSubscription = events.EquipmentChanged.Subscribe(_ => changes++);
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events, () => { },
			() => throw new System.IO.IOException("测试主动模拟写盘失败"), new Wallet());
		GD.Print("EXPECT: the following save error is intentionally injected by the test.");
		EquipmentResult result = events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, inventory.Slots[0]!.Equipment!.InstanceId));
		Check(result.Success && character.Equipment.Get(EquipmentSlot.Weapon)?.DefinitionId == weapon.Id && inventory.Slots[0] is null &&
			saveFailures == 1 && changes == 1,
			"save failure preserves committed equipment and still delivers both notifications");
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"  PASS: {message}");
	}
}
