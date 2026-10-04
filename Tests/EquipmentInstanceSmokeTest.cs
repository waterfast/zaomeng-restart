using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>独立测试装备个体、宝石事务、持久化和真实镶嵌界面，不访问玩家存档。</summary>
public partial class EquipmentInstanceSmokeTest : Node
{
	private static ItemCatalog CreateCatalog() => new()
	{
		Definitions =
		[
			new EquipmentDefinition { Id = "sword", DisplayName = "测试剑", Attack = 10, GemSocketCount = 2 },
			new GemDefinition { Id = "attack_gem", DisplayName = "测试攻击宝石", Bonus = 5 },
			new GemDefinition { Id = "health_gem", DisplayName = "测试生命宝石", Attribute = GemAttribute.Health, Bonus = 20 },
			new ItemDefinition { Id = "material", Category = ItemCategory.Material, MaxStack = 1 }
		]
	};

	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			CheckInstancesAndGems();
			await CheckGemUI();
			GD.Print("EQUIPMENT_INSTANCE_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"EQUIPMENT_INSTANCE_TEST: FAIL: {error}");
			GetTree().Paused = false;
			GetTree().Quit(1);
		}
	}

	private static void CheckInstancesAndGems()
	{
		ItemCatalog catalog = CreateCatalog();
		catalog.Validate();
		GameplayEvents events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		var character = new SaveCharacter { Id = "role_1", BaseStats = new() { Attack = 3, MaxHealth = 80 } };
		var inventory = new InventoryService(3, catalog);
		Check(inventory.AddItem("sword", 2), "acquisition creates one instance per equipment item");
		EquipmentInstance first = inventory.Slots[0]!.Equipment!;
		EquipmentInstance second = inventory.Slots[1]!.Equipment!;
		Check(first.InstanceId != second.InstanceId && first.DefinitionId == second.DefinitionId,
			"same definition produces distinct equipment identities");
		Check(!inventory.MoveStack(0, 1, 1), "same-name equipment cannot merge");
		Check(!inventory.RemoveItem("sword", 1), "definition-based removal cannot destroy an arbitrary equipment instance");
		inventory.AddItem("attack_gem", 2);
		int saves = 0, modifications = 0;
		CharacterStats stats = CharacterStatCalculator.Calculate(character, catalog);
		bool crossRequestRejected = false;
		using var observer = events.EquipmentModified.Subscribe(_ =>
		{
			modifications++;
			crossRequestRejected = events.EquipmentRequested.Send(new(EquipmentAction.Unequip)).Error == EquipmentError.Busy;
		});
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
			() => stats = CharacterStatCalculator.Calculate(character, catalog), () => saves++, new Wallet());
		GemResult Socket(string instanceId, int socket, int source, string gemId) =>
			events.GemRequested.Send(new(GemAction.Socket, instanceId, socket, source, gemId));
		GemResult Remove(string instanceId, int socket, string gemId) =>
			events.GemRequested.Send(new(GemAction.Remove, instanceId, socket, ExpectedGemId: gemId));

		var snapshot = inventory.Slots;
		Check(Socket(first.InstanceId, 0, 2, "attack_gem").Success, "socketing into equipment in backpack succeeds");
		Check(inventory.GetItemCount("attack_gem") == 1 && inventory.Slots[0]!.Equipment!.SocketedGemIds[0] == "attack_gem" &&
			inventory.Slots[1]!.Equipment!.SocketedGemIds[0] == "" && stats.Attack == 3,
			"socket consumes one gem and affects only the selected unequipped instance");
		Check(snapshot[0]!.Equipment!.SocketedGemIds[0] == "" && first.SocketedGemIds[0] == "",
			"previous inventory and event snapshots remain immutable");
		Check(crossRequestRejected && saves == 1 && modifications == 1, "gem changes save once and block cross-channel nested requests");
		Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, first.InstanceId)).Success && stats.Attack == 18,
			"equipping uses base equipment and its own gem bonuses");
		Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, 1, second.InstanceId)).Success && stats.Attack == 13,
			"another instance of the same definition can replace equipped gear");
		Check(character.Equipment.Get(EquipmentSlot.Weapon)?.InstanceId == second.InstanceId &&
			inventory.Slots[0]!.Equipment!.InstanceId == first.InstanceId && inventory.Slots[0]!.Equipment!.SocketedGemIds[0] == "attack_gem",
			"swap preserves both unique identities and the first item's gems");
		Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, second.InstanceId)).Error == EquipmentError.ItemChanged,
			"same-name stale instance selection is rejected");
		Check(Socket(second.InstanceId, 0, 2, "attack_gem").Success && stats.Attack == 18,
			"socketing into equipped gear updates live attributes");
		inventory.AddItem("material", 2);
		int savesBeforeFailure = saves, modificationsBeforeFailure = modifications;
		Check(Remove(second.InstanceId, 0, "attack_gem").Error == EquipmentError.InventoryFull,
			"full backpack rejects gem removal without deleting the gem");
		Check(stats.Attack == 18 && character.Equipment.Get(EquipmentSlot.Weapon)!.SocketedGemIds[0] == "attack_gem" &&
			saves == savesBeforeFailure && modifications == modificationsBeforeFailure,
			"failed gem transaction does not alter stats, equipment, save, or notifications");
		Check(Socket(first.InstanceId, 0, 1, "material").Error == EquipmentError.SocketOccupied,
			"occupied socket cannot silently overwrite an existing gem");
		Check(Socket(first.InstanceId, 1, 1, "material").Error == EquipmentError.NotGem,
			"ordinary materials cannot be used as gems");
		Check(Socket(first.InstanceId, 9, 1, "material").Error == EquipmentError.InvalidSocket,
			"invalid socket does not consume inventory");
		Check(Socket("unowned", 0, 1, "material").Error == EquipmentError.InstanceMissing,
			"unowned equipment cannot be modified");
		Check(Remove(second.InstanceId, 0, "health_gem").Error == EquipmentError.GemChanged,
			"stale expected gem cannot remove another gem");
		inventory.RemoveItem("material", 1);
		Check(Remove(second.InstanceId, 0, "attack_gem").Success && stats.Attack == 13 && inventory.GetItemCount("attack_gem") == 1,
			"gem removal returns the gem and removes its live bonuses");
		inventory.RemoveItem("material", 1);
		inventory.AddItem("health_gem", 1);
		Check(Socket(first.InstanceId, 1, 2, "health_gem").Success && stats.MaxHealth == 80,
			"two sockets are independent and backpack equipment does not grant bonuses");

		var serializer = new JsonSaveSerializer();
		GameSaveData data = InventorySaveMapper.Capture(inventory, new GameSaveData { Characters = [character] });
		GameSaveData loaded = serializer.Deserialize(serializer.Serialize(data));
		Check(loaded.Characters[0].Equipment.Get(EquipmentSlot.Weapon)!.InstanceId == second.InstanceId &&
			loaded.Inventory.Slots[0]!.Equipment!.InstanceId == first.InstanceId &&
			loaded.Inventory.Slots[0]!.Equipment!.SocketedGemIds.SequenceEqual(new[] { "attack_gem", "health_gem" }),
			"JSON round trip preserves unique IDs and every socket on both ownership locations");
		Check(SaveDataEditor.AddItem(loaded.Inventory, catalog, "material", 1) &&
			loaded.Inventory.Slots[0]!.Equipment!.SocketedGemIds.SequenceEqual(new[] { "attack_gem", "health_gem" }),
			"save editor retains instance data while changing other items");
		var restored = new InventoryService(3, catalog);
		InventorySaveMapper.Restore(loaded, restored);
		var service = new EquipmentService(loaded.Characters[0], catalog, restored);
		Check(service.Execute(new(EquipmentAction.Equip, 0, first.InstanceId)).Success &&
			CharacterStatCalculator.Calculate(loaded.Characters[0], catalog).MaxHealth == 100,
			"restored gear can be equipped with all gem effects intact");
		Check(restored.Slots[0]!.Equipment!.InstanceId == second.InstanceId,
			"restored swap returns the old instance without recreating it");

		character.Equipment.Set(EquipmentSlot.Armor, inventory.Slots[0]!.Equipment);
		bool duplicateRejected = false;
		try { serializer.Serialize(InventorySaveMapper.Capture(inventory, new GameSaveData { Characters = [character] })); }
		catch (InvalidDataException) { duplicateRejected = true; }
		Check(duplicateRejected, "save rejects an instance owned by inventory and a character at once");
		character.Equipment.Set(EquipmentSlot.Armor, null);
		var beforeInvalidRestore = inventory.Slots;
		bool invalidRestoreRejected = false;
		try { inventory.RestoreSlots([beforeInvalidRestore[0], beforeInvalidRestore[0], null]); }
		catch (ArgumentException) { invalidRestoreRejected = true; }
		Check(invalidRestoreRejected && inventory.Slots.SequenceEqual(beforeInvalidRestore),
			"duplicate-instance restore is rejected before mutating inventory");
	}

	private async Task CheckGemUI()
	{
		GetViewport().GuiEmbedSubwindows = true;
		ItemCatalog catalog = CreateCatalog();
		GameplayEvents events = GD.Load<GameplayEvents>("res://GameData/Events/GameplayEvents.tres");
		var character = new SaveCharacter { Id = "role_1", BaseStats = new() { Attack = 3, MaxHealth = 80 } };
		var inventory = new InventoryService(2, catalog);
		inventory.AddItem("sword", 1);
		inventory.AddItem("attack_gem", 1);
		string instanceId = inventory.Slots[0]!.Equipment!.InstanceId;
		var player = new Player();
		player.BindCharacter(character, catalog);
		using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events, player.RefreshCharacterStats, () => { }, new Wallet());
		Node2D backpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackPack.tscn").Instantiate<Node2D>();
		backpack.ProcessMode = ProcessModeEnum.Always;
		AddChild(backpack);
		using var adapter = new InventoryViewAdapter(inventory, catalog);
		using (var view = new LegacyBackpackView(backpack, adapter, catalog, character, new Wallet(), player, () => { }, events))
		{
			GetTree().Paused = true;
			Button itemButton = backpack.GetNode<GridContainer>(
				"Main_Backpack/MarginContainer/VBoxContainer/MarginContainer/Sc_Box/Gd_Box").GetChild<Button>(0);
			itemButton.EmitSignal(Control.SignalName.GuiInput,
				new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
			Window window = backpack.GetNode<Window>("GemSocketWindow");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(window.Visible, "right-click equipment opens the gem window during game pause");
			window.FindChild("Socket0", true, false).GetNode<Button>("Insert").EmitSignal(BaseButton.SignalName.Pressed);
			Check(inventory.Slots[0]!.Equipment!.SocketedGemIds[0] == "attack_gem" && inventory.GetItemCount("attack_gem") == 0,
				"actual socket button consumes a gem and updates the selected instance");
			window.FindChild("Socket0", true, false).GetNode<Button>("Remove").EmitSignal(BaseButton.SignalName.Pressed);
			Check(inventory.Slots[0]!.Equipment!.SocketedGemIds[0] == "" && inventory.GetItemCount("attack_gem") == 1,
				"actual removal button restores the gem to inventory");
			itemButton.EmitSignal(BaseButton.SignalName.Pressed);
			backpack.GetNode<Button>("ItemActionMenu/Options/VBoxContainer/equip").EmitSignal(BaseButton.SignalName.Pressed);
			window.FindChild("Socket0", true, false).GetNode<Button>("Insert").EmitSignal(BaseButton.SignalName.Pressed);
			Check(character.Equipment.Get(EquipmentSlot.Weapon)!.InstanceId == instanceId && player.Attack == 18,
				"gem window follows the same instance after equipping and updates player stats");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/gem-socket-preview.png");
			}
			backpack.GetNode<Button>("background/infomation/equ_/HBoxContainer/wq").EmitSignal(BaseButton.SignalName.Pressed);
			backpack.GetNode<Button>("ItemActionMenu/Options/VBoxContainer/unequip").EmitSignal(BaseButton.SignalName.Pressed);
			Check(inventory.Slots[0]!.Equipment!.InstanceId == instanceId && inventory.Slots[0]!.Equipment!.SocketedGemIds[0] == "attack_gem",
				"unequipping from the actual UI preserves socketed gems");
			itemButton.EmitSignal(BaseButton.SignalName.MouseEntered);
			Check(backpack.GetNode<Label>("ItemTooltip/pro_wk/information/inf/SocketSummary").Text.Contains("测试攻击宝石"),
				"tooltip displays the selected instance's socket contents");
			backpack.Hide();
			Check(!window.Visible, "closing backpack also closes gem window");
			GetTree().Paused = false;
		}
		Check(events.EquipmentModified.ListenerCount == 0 && events.EquipmentChanged.ListenerCount == 0,
			"disposing both views removes all equipment observers");
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
