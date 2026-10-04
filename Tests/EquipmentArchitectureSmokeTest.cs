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

/// <summary>验证调用顺序和失败边界，不向玩家真实存档写入测试装备。</summary>
public partial class EquipmentArchitectureSmokeTest : Node
{
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			GetTree().Paused = true;
			ItemCatalog catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			catalog.Validate();
			Check(catalog.TryGetDefinition("ryjgb", out ItemDefinition? item) && item is EquipmentDefinition,
				"resource inheritance loads through existing catalog");
			var character = new SaveCharacter { Id = "role_1", BaseStats = new() { MaxHealth = 80, Attack = 8 } };
			Player player = GetParent().GetNode<Player>("Player");
			player.BindCharacter(character, catalog);
			var inventory = new InventoryService(1, catalog);
			var events = new GameplayEvents
			{
				EquipmentRequested = new(), EquipmentChanged = new(), InventoryChanged = new(), SaveFailed = new(),
				GemRequested = new(), EquipmentModified = new(), ItemActionRequested = new(), ItemActionCompleted = new()
			};
			int saves = 0;
			byte[] json = [];
			using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
				player.RefreshCharacterStats, () =>
			{
				Check(player.Attack == CharacterStatCalculator.Calculate(character, catalog).Attack,
					"save runs after applying recalculated stats");
				json = new JsonSaveSerializer().Serialize(new GameSaveData
				{
					Characters = [character],
					Inventory = new InventorySaveData { Capacity = 1, Slots = [null] }
				});
				saves++;
			}, new Wallet());
			EquipmentResult Equip(string id) => events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0,
				inventory.Slots[0]?.ItemId == id ? inventory.Slots[0]!.Equipment!.InstanceId : "missing"), this);
			Check(!Equip("ryjgb").Success && saves == 0 && character.Equipment.Get(EquipmentSlot.Weapon) is null,
				"rejected exchange does not mutate loadout or save");
			inventory.AddItem("ryjgb", 1);
			Check(Equip("ryjgb").Success && player.Attack == 83 && saves == 1,
				"equipment changes recalculate and save once");
			Check(new JsonSaveSerializer().Deserialize(json).Characters[0].Equipment.Get(EquipmentSlot.Weapon)?.DefinitionId == "ryjgb" &&
				character.BaseStats.Attack == 8, "save records equipment without polluting base stats");
			Check(!Equip("ryjgb").Success && saves == 1, "stale selection causes no extra save");
			Check(!Equip("missing").Success && saves == 1, "unknown equipment is rejected");
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip, Slot: EquipmentSlot.Weapon), this).Success
				&& player.Attack == 8 && saves == 2,
				"unequip removes bonuses and saves once");
			EquipmentDefinition weapon = (EquipmentDefinition)item!;
			character.Id = "role_2";
			Check(!Equip(weapon.Id).Success, "character restriction is enforced");
			character.Id = "role_1";
			var extra = new EquipmentDefinition { Id = "probe_armor", Slot = EquipmentSlot.Armor,
				HealthBonus = 50, MagicDefenseBonus = 12, LifeStealBonus = 0.1f };
			catalog = new ItemCatalog { Definitions = [extra] };
			character.Equipment.Set(EquipmentSlot.Weapon, null);
			character.Equipment.Set(EquipmentSlot.Armor, EquipmentInstance.Create(extra.Id, 0));
			CharacterStats calculated = CharacterStatCalculator.Calculate(character, catalog);
			Check(calculated.MaxHealth == 130 && calculated.MagicDefense == 12 && calculated.LifeSteal == 0.1f,
				"new equipment contributes all supported stats");

			Node2D backpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackPack.tscn").Instantiate<Node2D>();
			AddChild(backpack);
			var registry = CharacterStatRegistry.CreateDefault();
			using var presenter = new CharacterStatsPresenter(backpack, character, player, catalog, registry);
			for (int i = 0; i < 5; i++) registry.Register(new($"extra_{i}", $"新增{i}", s => s.Attack));
			Check(presenter.PageCount == 3, "registered attributes automatically create a third page");
			presenter.ShowPage(3);
			Check(backpack.GetNode<Label>("background/infomation/hp/Hp_tt").Text == "新增4",
				"third page displays registered data");
			registry.Register(new("extra_4", "替换", _ => 42));
			Check(registry.Entries.Count == 21 && backpack.GetNode<Label>("background/infomation/hp").Text == "42",
				"same ID replaces definition and refreshes display");
			registry.Unregister("extra_4");
			Check(presenter.PageCount == 2, "unregister clamps current page");
			var tooltip = new LegacyItemTooltip(backpack);
			Vector2 initialViewport = GetViewport().GetVisibleRect().Size;
			tooltip.Show(weapon, 1, new Rect2(initialViewport.X - 260, initialViewport.Y - 80, 40, 40));
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Node2D tooltipRoot = backpack.GetNode<Node2D>("ItemTooltip");
			const string path = "pro_wk/information/inf/";
			Check(tooltipRoot.GetNode<Label>(path + "VBoxContainer2/eq_pz").Text == "品质：精良" &&
				tooltipRoot.GetNode<Label>(path + "VBoxContainer3/eq_power").Text == "攻击：75" &&
				!tooltipRoot.GetNode<Label>(path + "VBoxContainer3/eq_hp").Visible,
				"legacy tooltip shows metadata and hides zero attributes");
			ColorRect background = tooltipRoot.GetNode<ColorRect>("ColorRect");
			Vector2 bottom = background.GetGlobalTransformWithCanvas() * background.Size;
			Vector2 viewport = GetViewport().GetVisibleRect().Size;
			Check(bottom.X <= viewport.X && bottom.Y <= viewport.Y, "tooltip is clamped using actual layout size");
			Rect2 slotArea = new(200, 150, 50, 50);
			tooltip.Show(weapon, 1, slotArea);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Rect2 popup = background.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, background.Size);
			Check(popup.Position.X >= slotArea.End.X + 8 && !popup.Intersects(slotArea),
				"tooltip stays to the right when there is space");
			slotArea = new Rect2(viewport.X - 220, 150, 50, 50);
			tooltip.Show(weapon, 1, slotArea);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			popup = background.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, background.Size);
			Check(popup.Position.X >= slotArea.End.X + 8 && popup.End.X <= viewport.X - 8 && !popup.Intersects(slotArea),
				"tooltip stays beside the last slot on its right and narrows to fit the screen");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://Tests/equipment-architecture-preview.png");
			}
			GD.Print("EQUIPMENT_ARCHITECTURE_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"EQUIPMENT_ARCHITECTURE_TEST: FAIL: {error}");
			GetTree().Quit(1);
		}
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"  PASS: {message}");
	}
}
