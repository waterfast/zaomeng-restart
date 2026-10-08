using System;
using System.Linq;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Combat.Effects;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.UI.Inventory;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

/// <summary>只使用内存角色和背包，验证真实命中与换装链，不访问玩家存档。</summary>
public partial class EquipmentSkillsSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			TranslationServer.SetLocale("zh_CN");
			ItemCatalog catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			catalog.Validate();
			var skills = GD.Load<Zaomeng.Skills.SkillCatalog>("res://Content/Skills/Wukong/Catalog.tres");
			Check(skills.Skills.All(skill => skill.Id != "dptxj"), "大品天仙决只由装备授予，不登记可学习主动技能");
			catalog.TryGetDefinition("ryjgb", out ItemDefinition? item);
			var weapon = (EquipmentDefinition)item!;
			Check(weapon.GrantedSkills.Count == 1 && weapon.Description.Contains("定海神针"), "game data and Chinese lore load");
			var character = new SaveCharacter { Id = "role_1", BaseStats = new() { MaxHealth = 1000, MaxMana = 500 } };
			Player player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.BindCharacter(character, catalog);
			player.InputEnabled = false;
			AddChild(player);
			player.SetPhysicsProcess(false);
			Monster target = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			target.AiEnabled = false;
			target.MaxHealth = 10000;
			target.ExperienceReward = 0;
			target.SoulValuePerOrb = 0;
			AddChild(target);
			target.SetPhysicsProcess(false);
			var events = new GameplayEvents { EquipmentRequested = new(), EquipmentChanged = new(), InventoryChanged = new(),
				SaveFailed = new(), GemRequested = new(), EquipmentModified = new(), ItemActionRequested = new(), ItemActionCompleted = new() };
			var inventory = new InventoryService(4, catalog);
			int saves = 0;
			using var binding = new GameplayEquipmentBinding(character, catalog, inventory, events,
				player.RefreshCharacterStats, () => { Check(player.GrantedEquipmentSkills.Count == (character.Equipment.Get(EquipmentSlot.Weapon) is null ? 0 : 1), "skills synchronized before save"); saves++; }, new Wallet());
			inventory.AddItem(weapon.Id, 1);
			Check(player.GrantedEquipmentSkills.Count == 0, "carrying equipment does not grant skills");
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Equip, 0, inventory.Slots[0]!.Equipment!.InstanceId), this).Success,
				"equip through request channel");
			Check(player.GrantedEquipmentSkills.Count == 1 && character.SkillLevels.Count == 0, "equipment grants separate from learned skills");
			player.ReceiveHit(new HitResult(600, Vector2.Zero, 0));
			player.TrySpendMana(400);
			player.CriticalRating = 1e20f; // 换算并四舍五入后暴击概率为 1，消除测试随机性。
			var critical = new HitDefinition { AttackMultiplier = 0, FlatDamage = 10, CanCrit = true, DamageType = DamageType.True };
			Check(CombatResolver.Resolve(player, target, critical), "critical hit resolves");
			Check(Near(player.Health, 600) && Near(player.Mana, 200), "critical restores 20 percent of maximum health and mana");
			for (int i = 0; i < 5; i++) player.RefreshCharacterStats();
			player.CriticalRating = 1e20f;
			CombatResolver.Resolve(player, target, critical);
			Check(Near(player.Health, 800) && Near(player.Mana, 300), "repeated refresh does not multiply recovery");
			var normal = new HitDefinition { AttackMultiplier = 0, FlatDamage = 10, CanCrit = false, DamageType = DamageType.True };
			CombatResolver.Resolve(player, target, normal);
			Check(Near(player.Health, 800) && Near(player.Mana, 300), "noncritical hit does not recover");
			target.DodgeRating = 1e20f;
			CombatResolver.Resolve(player, target, critical);
			Check(Near(player.Mana, 300), "miss does not trigger recovery");
			target.DodgeRating = 0;
			CombatResolver.Resolve(player, target, new HitDefinition { AttackMultiplier = 0, CanCrit = true });
			Check(Near(player.Mana, 300), "zero damage does not trigger recovery");
			int enemyTeam = target.Team;
			target.Team = player.Team;
			Check(!CombatResolver.Resolve(player, target, critical) && Near(player.Mana, 300), "friendly target does not trigger recovery");
			target.Team = enemyTeam;
			Player protectedTarget = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			protectedTarget.Team = enemyTeam;
			protectedTarget.InputEnabled = false;
			AddChild(protectedTarget);
			protectedTarget.SetPhysicsProcess(false);
			Check(protectedTarget.TryUseSkill(new SkillDefinition { Animation = "wait", Invulnerable = true }), "invulnerable test target starts action");
			Check(!CombatResolver.Resolve(player, protectedTarget, critical) && Near(player.Mana, 300), "invulnerable target does not trigger recovery");
			CombatResolver.Resolve(player, target, critical);
			CombatResolver.Resolve(player, target, critical);
			CombatResolver.Resolve(player, target, critical);
			Check(Near(player.Health, 1000) && Near(player.Mana, 500), "recovery clamps to both limits");

			// 同一效果来自多件装备时去重；不同定义冒用同一 ID 则在目录验证时拒绝。
			var accessory = new EquipmentDefinition { Id = "test_accessory", Slot = EquipmentSlot.Accessory, GrantedSkills = [weapon.GrantedSkills[0]] };
			catalog.Definitions.Add(accessory);
			character.Equipment.Set(EquipmentSlot.Accessory, EquipmentInstance.Create(accessory.Id, 0));
			player.RefreshCharacterStats();
			Check(player.GrantedEquipmentSkills.Count == 1, "shared skill granted by multiple equipment is deduplicated");
			var secondSkill = new PassiveSkillDefinition { Id = "test_second_skill", NameKey = weapon.GrantedSkills[0].NameKey,
				DescriptionKey = weapon.GrantedSkills[0].DescriptionKey, Effect = new CriticalResourceRecoveryEffect { HealthFraction = 0.1f, ManaFraction = 0 } };
			accessory.GrantedSkills.Add(secondSkill);
			player.RefreshCharacterStats();
			Check(player.GrantedEquipmentSkills.Count == 2, "one equipment can grant multiple distinct skills");
			player.ReceiveHit(new HitResult(300, Vector2.Zero, 0, DamageType.True));
			player.TrySpendMana(100);
			player.CriticalRating = 1e20f;
			CombatResolver.Resolve(player, target, critical);
			Check(Near(player.Health, 1000) && Near(player.Mana, 500), "distinct equipped skills both execute on the same critical hit");
			var preview = new EquipmentSkillListView();
			preview.ShowSkills(accessory);
			Check(preview.GetChildren().OfType<VBoxContainer>().Count() == 2 && secondSkill.MaximumLevel == 1, "all equipment skills have their own icon rows and default level cap one");
			preview.Free();
			accessory.GrantedSkills.Remove(secondSkill);
			character.Equipment.Set(EquipmentSlot.Accessory, null);
			player.RefreshCharacterStats();
			Check(events.EquipmentRequested.Send(new(EquipmentAction.Unequip, Slot: EquipmentSlot.Weapon), this).Success && saves == 2,
				"unequip through request channel");
			player.ReceiveHit(new HitResult(400, Vector2.Zero, 0));
			player.TrySpendMana(200);
			player.CriticalRating = 1e20f;
			CombatResolver.Resolve(player, target, critical);
			Check(player.GrantedEquipmentSkills.Count == 0 && Near(player.Health, 600) && Near(player.Mana, 300), "unequip immediately removes recovery");
			character.Equipment.Set(EquipmentSlot.Weapon, EquipmentInstance.Create(weapon.Id, 0));
			player.RefreshCharacterStats();
			player.CriticalRating = 1e20f;
			CombatResolver.Resolve(player, target, new HitDefinition { AttackMultiplier = 0, FlatDamage = 100000, CanCrit = true, DamageType = DamageType.True });
			Check(target.IsDead && Near(player.Health, 800) && Near(player.Mana, 400), "lethal critical also triggers recovery");
			Check(!CombatResolver.Resolve(player, target, critical) && Near(player.Mana, 400), "corpse does not trigger again");
			player.BindCharacter(new SaveCharacter { Id = "role_2", BaseStats = new() { MaxHealth = 1000, MaxMana = 500 } }, catalog);
			Check(player.GrantedEquipmentSkills.Count == 0, "character switch removes previous equipment skills");

			Node2D backpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackPack.tscn").Instantiate<Node2D>();
			AddChild(backpack);
			var tooltip = new LegacyItemTooltip(backpack);
			tooltip.Show(weapon, 1, new Rect2(300, 100, 40, 40));
			Label description = backpack.GetNode<Label>("ItemTooltip/pro_wk/information/inf/VBoxContainer/eq_ms");
			Check(ReadLabels(description.GetParent().GetNode("EquipmentSkills")).Contains("大品天仙决") && ReadLabels(description.GetParent().GetNode("EquipmentSkills")).Contains("20%"), "backpack reads skill name and recovery parameters");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var tooltipLayout = backpack.GetNode<MarginContainer>("ItemTooltip/pro_wk");
			Check(tooltipLayout.Size.Y < GetViewportRect().Size.Y, "skill description fits the tooltip viewport");
			var modernBackpack = GD.Load<PackedScene>("res://Scenes/UI/BackPack/BackpackView.tscn").Instantiate<BackpackView>();
			AddChild(modernBackpack);
			using var adapter = new InventoryViewAdapter(inventory, catalog);
			modernBackpack.Bind(adapter);
			Label modernDescription = modernBackpack.GetNode<Label>("Detail/Description");
			Check(ReadLabels(modernDescription.GetParent().GetNode("EquipmentSkills")).Contains("大品天仙决"), "detail panel uses the same equipment skill data");
			TranslationServer.SetLocale("en");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(ReadLabels(description.GetParent().GetNode("EquipmentSkills")).Contains("Great Heavenly Immortal Art") && ReadLabels(description.GetParent().GetNode("EquipmentSkills")).Contains("20%") && description.Text.Contains("sea-calming"), "open tooltip refreshes to English");
			Check(ReadLabels(modernDescription.GetParent().GetNode("EquipmentSkills")).Contains("Great Heavenly Immortal Art"), "detail panel also refreshes to English");
			var effect = (CriticalResourceRecoveryEffect)weapon.GrantedSkills[0].Effect;
			float configuredHealth = effect.HealthFraction;
			effect.HealthFraction = 0.35f;
			Check(ItemDescriptionBuilder.Build(weapon).Contains("35%"), "description comes from actual effect parameters");
			effect.HealthFraction = configuredHealth;
			foreach (ItemDefinition entry in catalog.Definitions)
				Check(entry.DescriptionKey.Length == 0 || entry.Description != entry.DescriptionKey, $"localized description: {entry.Id}");
			var invalid = new CriticalResourceRecoveryEffect { HealthFraction = float.NaN };
			CheckThrows(invalid.Validate, "nonfinite effect rejected");
			accessory.GrantedSkills.Add(weapon.GrantedSkills[0]);
			CheckThrows(catalog.Validate, "duplicate skill on one equipment rejected");
			accessory.GrantedSkills.RemoveAt(1);
			accessory.GrantedSkills[0] = new PassiveSkillDefinition { Id = weapon.GrantedSkills[0].Id, NameKey = "TEST_NAME", DescriptionKey = "TEST_DESCRIPTION", Effect = new CriticalResourceRecoveryEffect() };
			CheckThrows(catalog.Validate, "different definitions with same skill ID rejected");
			player.ReceiveHit(new HitResult(player.MaxHealth + 1, Vector2.Zero, 0));
			float manaAtDeath = player.Mana;
			weapon.GrantedSkills[0].Effect.OnHitDealt(player, target, new HitResult(10, Vector2.Zero, 0, Critical: true));
			Check(player.IsDead && player.Health == 0 && player.Mana == manaAtDeath, "effect cannot revive or restore a dead owner");
			GD.Print("EQUIPMENT SKILLS SMOKE TEST PASSED");
			foreach (Node child in GetChildren()) child.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}

	private static bool Near(float left, float right) => Mathf.IsEqualApprox(left, right);
	private static string ReadLabels(Node node)
	{
		string text = node is Label label ? label.Text : "";
		foreach (Node child in node.GetChildren()) text += "\n" + ReadLabels(child);
		return text;
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
	private static void CheckThrows(Action action, string message)
	{
		try { action(); } catch (InvalidOperationException) { GD.Print($"PASS: {message}"); return; }
		throw new InvalidOperationException(message);
	}
}
