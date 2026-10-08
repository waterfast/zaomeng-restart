using System;
using System.Linq;
using Godot;
using Zaomeng;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Save;
using Zaomeng.Settings;
using SaveCharacter = Zaomeng.Character.Character;

public partial class GroundLootVisualsSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			GameSettings.Load("user://ground_loot_visuals_test.cfg");
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			catalog.Validate();
			foreach (var item in catalog.Definitions.OfType<Zaomeng.Equipment.EquipmentDefinition>())
			{
				Check(item.GroundIcon is not null && item.GroundIcon != item.Icon, $"{item.Id}独立绑定地面图");
				Image image = item.GroundIcon!.GetImage();
				if (image.IsCompressed()) image.Decompress();
				Check(image.GetPixel(0, 0).A == 0, $"{item.Id}地面图角落透明");
			}
			var character = new SaveCharacter { Id = "role_1", Name = "显示测试" };
			CharacterProgression.SyncBaseStats(character);
			var inventory = new InventoryService(25, catalog);
			var data = InventorySaveMapper.Capture(inventory);
			data.Characters.Add(character);
			data.CurrentCharacterId = character.Id;
			foreach (var (property, value) in new (string, object)[] { ("Slot", 0), ("Data", data), ("Inventory", inventory) })
				typeof(GameSession).GetProperty(property)!.SetValue(null, value);
			var level = GD.Load<PackedScene>("res://Scenes/Level/Level_1.tscn").Instantiate<GameplayLevel>();
			AddChild(level);
			level.GetChildren().OfType<WaveEncounter>().Single().SetPhysicsProcess(false);
			var player = level.GetNode<Player>("Player");
			player.InputEnabled = false;
			player.SetPhysicsProcess(false);
			player.Position = new(460, 502);
			var definition = GD.Load<MonsterDefinition>("res://Content/Monsters/Templates/monkey.tres");
			var pool = new MonsterPool(level.GetNode("Enemies"));
			var monster = pool.Spawn(definition, new(680, 502));
			monster.SetPhysicsProcess(false);
			monster.ReceiveHit(new HitResult(30, Vector2.Zero, 0, DamageType.True));
			catalog.TryGetDefinition("ptcf", out var cloth);
			var pickup = GD.Load<PackedScene>("res://Scenes/Effects/ItemPickup.tscn").Instantiate<ItemPickup>();
			pickup.Configure(cloth!, 1, player, _ => false);
			pickup.Position = new(550, 478);
			level.AddChild(pickup);
			for (int frame = 0; frame < 90; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			var bar = monster.GetNode<TextureProgressBar>("PresentationSettings/MonsterHealthBar/blood_bar");
			Check(Mathf.IsEqualApprox((float)bar.Value, 0.5f) && monster.GetNode("PresentationSettings/MonsterHealthBar").FindChildren("*", "Label", true, false).Count == 0,
				"头顶细红条反映半血，且没有数字节点");
			if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--capture-loot"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				DirAccess.MakeDirRecursiveAbsolute("res://.godot/drop-checks");
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/drop-checks/restored-visuals.png");
			}
			monster.ResetForSpawn(new(680, 502));
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(bar.Value == 1, "怪物重新出生恢复满血条");
			monster.ReceiveHit(new HitResult(10000, Vector2.Zero, 0, DamageType.True));
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(!monster.GetNode<Node2D>("PresentationSettings/MonsterHealthBar").Visible, "死亡隐藏头顶血条");
			GD.Print("GroundLootVisualsSmokeTest PASS");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError($"GroundLootVisualsSmokeTest FAIL: {error}"); GetTree().Quit(1); }
		finally { GameSettings.Load(); }
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
