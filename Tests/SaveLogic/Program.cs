using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.Save.Storage;

string root = Path.Combine(Path.GetTempPath(), "zaomeng-save-tests-" + Guid.NewGuid().ToString("N"));
try
{
	var catalog = new TestCatalog(new Dictionary<string, int> { ["herb"] = 10, ["sword"] = 1 });
	var inventory = new InventoryService(3, catalog);
	inventory.RestoreSlots(new ItemStack?[] { null, new("herb", 7), new("sword", 1) });
	Check(inventory.Slots[0] is null && inventory.Slots[1] == new ItemStack("herb", 7),
		"restore keeps empty slots and ordering");
	int changes = 0;
	inventory.Changed += () => changes++;
	Expect<ArgumentException>(() => inventory.RestoreSlots(new ItemStack?[] { null, new("missing", 1), null }));
	Check(changes == 0 && inventory.Slots[1] == new ItemStack("herb", 7),
		"invalid restore leaves inventory unchanged");
	Expect<ArgumentException>(() => inventory.RestoreSlots(new ItemStack?[] { null, new("herb", 11), null }));
	Check(changes == 0, "oversized stack rejected");

	var serializer = new JsonSaveSerializer();
	var data = new GameSaveData
	{
		Inventory = new InventorySaveData
		{
			Capacity = 3,
			Slots = [null, new ItemStackSaveData { ItemId = "herb", Count = 7 },
				new ItemStackSaveData { ItemId = "sword", Count = 1 }]
		}
	};
	Check(SaveDataEditor.AddItem(data.Inventory, catalog, "herb", 5), "save editor adds item through inventory rules");
	Check(data.Inventory.Slots[0]?.Count == 2 && data.Inventory.Slots[1]?.Count == 10,
		"save editor fills existing stack before empty slot");
	Check(!SaveDataEditor.AddItem(data.Inventory, catalog, "sword", 1),
		"save editor rejects addition when no slot remains");
	Check(!SaveDataEditor.AddItem(data.Inventory, catalog, "missing", 1),
		"save editor rejects unknown item");
	Check(!SaveDataEditor.RemoveItem(data.Inventory, catalog, "herb", 13),
		"save editor rejects insufficient quantity without mutation");
	Check(data.Inventory.Slots[0]?.Count == 2 && data.Inventory.Slots[1]?.Count == 10,
		"failed save edit retains prior slots");
	SaveDataEditor.ClearSlot(data.Inventory, catalog, 0);
	Check(data.Inventory.Slots[0] is null && data.Inventory.Slots[1]?.Count == 10,
		"save editor clears selected slot");
	Check(SaveDataEditor.RemoveItem(data.Inventory, catalog, "herb", 3),
		"save editor subtracts items");
	var restoredInventory = new InventoryService(3, catalog);
	InventorySaveMapper.Restore(data, restoredInventory);
	Check(restoredInventory.Slots[1] == new ItemStack("herb", 7),
		"persistent inventory restores without a scene snapshot");
	GameSaveData captured = InventorySaveMapper.Capture(restoredInventory, data);
	Check(ReferenceEquals(captured, data) && captured.Inventory.Slots[1]?.Count == 7,
		"inventory capture keeps character and wallet data");
	var wukong = new Character { Id = "wukong", Name = "悟空", BaseStats = new CharacterStats { Attack = 12, MaxHealth = 100 } };
	var bajie = new Character { Id = "bajie", Name = "八戒", BaseStats = new CharacterStats { Attack = 8, MaxHealth = 140 } };
	wukong.LearnSkill("rising_dragon", 2);
	wukong.EquipSkill(0, "rising_dragon");
	Expect<InvalidOperationException>(() => bajie.EquipSkill(0, "rising_dragon"));
	data.Characters.Add(wukong);
	data.Characters.Add(bajie);
	data.Wallet.Add(CurrencyType.Soul, 100);
	data.Wallet.Add(CurrencyType.Coupon, 5);
	Check(data.Wallet.TrySpend(CurrencyType.Soul, 30) && data.Wallet.Souls == 70,
		"characters use one save-level wallet");
	Check(!data.Wallet.TrySpend(CurrencyType.Coupon, 6) && data.Wallet.Coupons == 5,
		"failed spend leaves shared balance unchanged");
	var plain = new SaveManager(serializer, new PlainFileSaveStorage(Path.Combine(root, "plain")));
	plain.Save(1, data);
	Check(plain.Load(1).Inventory.Slots[1]?.Count == 7, "plain JSON round trip");
	GameSaveData loaded = plain.Load(1);
	Check(loaded.Characters.Count == 2 && loaded.Characters[0].BaseStats.Attack == 12 &&
		loaded.Characters[0].EquippedSkillIds[0] == "rising_dragon" && loaded.Wallet.Souls == 70,
		"character and shared wallet round trip");
	var legacy = serializer.Deserialize(
		"{\"version\":1,\"player\":{\"scenePath\":\"res://Scenes/TestArena.tscn\",\"health\":80,\"facingDirection\":1},\"inventory\":{\"capacity\":1,\"slots\":[null]}}"u8.ToArray());
	Check(legacy.Wallet.Souls == 0 && legacy.Characters.Count == 0,
		"older version 1 saves gain empty character and wallet data");
	Check(!Encoding.UTF8.GetString(serializer.Serialize(legacy)).Contains("\"player\"", StringComparison.Ordinal),
		"legacy scene snapshot is discarded when saved again");
	wukong.EquippedSkillIds[0] = "unknown";
	Expect<InvalidDataException>(() => serializer.Serialize(data));
	wukong.EquippedSkillIds[0] = "rising_dragon";
	Check(File.ReadAllText(Path.Combine(root, "plain", "save_01.json")).Contains("\"itemId\""),
		"development file is readable JSON");
	data.Wallet.Souls = 60;
	plain.Save(1, data);
	File.WriteAllText(Path.Combine(root, "plain", "save_01.json"), "corrupt");
	Check(plain.Load(1).Wallet.Souls == 70, "invalid primary JSON falls back to backup");
	data.Wallet.Souls = 40;
	plain.Save(1, data);
	Check(plain.Load(1).Wallet.Souls == 40, "save replaces damaged primary");
	Check(serializer.Deserialize(File.ReadAllBytes(Path.Combine(root, "plain", "save_01.json.bak")))
		.Wallet.Souls == 70, "save preserves good backup after damaged primary");

	byte[] key = RandomNumberGenerator.GetBytes(32);
	string secureRoot = Path.Combine(root, "secure");
	var encrypted = new SaveManager(serializer, new EncryptedFileSaveStorage(secureRoot, key));
	data.Wallet.Souls = 70;
	encrypted.Save(1, data);
	byte[] firstFile = File.ReadAllBytes(Path.Combine(secureRoot, "save_01.dat"));
	Check(!Encoding.UTF8.GetString(firstFile).Contains("itemId", StringComparison.Ordinal),
		"encrypted file hides JSON text");
	data.Wallet.Souls = 60;
	encrypted.Save(1, data);
	byte[] secondFile = File.ReadAllBytes(Path.Combine(secureRoot, "save_01.dat"));
	Check(!firstFile.SequenceEqual(secondFile), "save uses a fresh IV");
	Check(encrypted.Load(1).Wallet.Souls == 60, "encrypted round trip");
	secondFile[30] ^= 1;
	File.WriteAllBytes(Path.Combine(secureRoot, "save_01.dat"), secondFile);
	Expect<CryptographicException>(() => new EncryptedFileSaveStorage(secureRoot, key).Read(1));
	Check(encrypted.Load(1).Wallet.Souls == 70, "tampered primary falls back to backup");
	Expect<InvalidDataException>(() => new SaveManager(serializer,
		new EncryptedFileSaveStorage(secureRoot, RandomNumberGenerator.GetBytes(32))).Load(1));
	Expect<NotSupportedException>(() => serializer.Deserialize(
		"{\"version\":2,\"inventory\":{}}"u8.ToArray()));
	Console.WriteLine("Save logic tests passed.");
}
finally
{
	if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static void Check(bool condition, string description)
{
	if (!condition) throw new Exception("FAILED: " + description);
}

static void Expect<T>(Action action) where T : Exception
{
	try { action(); }
	catch (T) { return; }
	throw new Exception("Expected " + typeof(T).Name);
}

sealed class TestCatalog(IReadOnlyDictionary<string, int> maxStacks) : IItemCatalog
{
	public bool TryGetMaxStack(string itemId, out int maxStack) => maxStacks.TryGetValue(itemId, out maxStack);
}
