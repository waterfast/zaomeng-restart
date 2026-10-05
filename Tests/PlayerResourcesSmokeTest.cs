using System;
using Godot;
using Zaomeng.Character;
using Zaomeng.Items;
using Zaomeng.UI;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

/// <summary>独立运行，不读取或修改真实存档。</summary>
public partial class PlayerResourcesSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			var character = new SaveCharacter { Id = "role_1", Name = "孙悟空" };
			CharacterProgression.SyncBaseStats(character);
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.BindCharacter(character, catalog);
			player.InputEnabled = false;
			AddChild(player);
			player.Position = new Vector2(350, 400);
			player.Gravity = 0;
			Check(player.Mana == 50 && player.MaxMana == 50, "initial mana");
			Check(CharacterProgression.ExperienceToNextLevel(19) == 4000 &&
				CharacterProgression.ExperienceToNextLevel(20) == 10000, "legacy experience boundary");
			SkillDefinition skill = player.EquippedSkill2!;
			character.LearnSkill(skill.Id);
			Check(player.TryUseSkill(skill) && player.Mana == 15, "skill costs mana after starting");
			Check(player.GetSkillCooldown(skill) > 0 && !player.TryUseSkill(skill) && player.Mana == 15,
				"cooldown rejects repeat without charging");
			player.ResetForSpawn(player.Position);
			Check(!player.TryUseSkill(skill) && player.Mana == 15, "cooldown survives interruption");
			Check(!player.TrySpendMana(16) && player.Mana == 15, "insufficient mana rejected");
			player.RestoreMana(100);
			Check(player.Mana == 50, "restoration clamps to maximum");
			character.BaseStats.MaxMana = 10;
			player.RefreshCharacterStats();
			Check(player.Mana == 10 && player.MaxMana == 10, "stat refresh clamps reduced mana limit");
			CharacterProgression.SyncBaseStats(character);
			player.RefreshCharacterStats();
			Check(player.Mana == 10 && player.MaxMana == 50, "increased limit does not grant free mana");
			player.RestoreMana(100);
			float cooldownBeforePause = player.GetSkillCooldown(skill);
			GetTree().Paused = true;
			await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
			Check(player.GetSkillCooldown(skill) == cooldownBeforePause && player.Mana == 50,
				"pause freezes player resource timers");
			GetTree().Paused = false;
			character.PermanentBonuses.ManaRegeneration = 2;
			player.RefreshCharacterStats();
			player.TrySpendMana(10);
			await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
			Check(player.Mana == 42, "one second regeneration uses calculated stats");
			int notifications = 0;
			player.ProgressionChanged += () => notifications++;
			player.GainExperience(139);
			Check(player.Level == 1 && player.Experience == 139, "experience below threshold");
			player.GainExperience(11);
			Check(player.Level == 2 && player.Experience == 0 && player.MaxMana == 65 && player.MaxHealth == 130,
				"legacy level growth and clearing surplus experience");
			Check(player.Health == player.MaxHealth && player.Mana == player.MaxMana, "level growth fully replenishes resources");
			var monster = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			monster.AiEnabled = false;
			AddChild(monster);
			monster.SetPhysicsProcess(false);
			monster.ExperienceReward = 5;
			var lethal = new HitDefinition { DamageType = DamageType.True, FlatDamage = 10000, CanCrit = false };
			Check(CombatResolver.Resolve(player, monster, lethal) && monster.IsDead && player.Experience == 5,
				"lethal hit awards monster experience");
			Check(!CombatResolver.Resolve(player, monster, lethal) && player.Experience == 5,
				"corpse cannot award experience twice");
			var hud = GD.Load<PackedScene>("res://Scenes/UI/Level/Role_information.tscn").Instantiate<Node2D>();
			AddChild(hud);
			var presenter = new PlayerHud();
			presenter.Bind(hud, player);
			hud.AddChild(presenter);
			Check(hud.GetNode<Sprite2D>("roleLayer/role_head").Texture is not null &&
				hud.GetNode<Label>("roleLayer/role_hp_mp_exp/mp_bar/mp_text").Text == "65/65" &&
				hud.GetNode<Label>("roleLayer/role_hp_mp_exp/exp_bar/exp_text").Text == "5/160", "HUD displays player resources");
			Check(notifications == 3, "progression notification once per reward");
			character.Level = 54;
			CharacterProgression.SyncBaseStats(character);
			player.RefreshCharacterStats();
			player.GainExperience(player.ExperienceToNextLevel);
			player.GainExperience(10000);
			Check(player.Level == 55 && player.Experience == 0, "maximum level stops experience gain");
			player.ReceiveHit(new HitResult(player.MaxHealth + 1, Vector2.Zero, 0));
			float manaAtDeath = player.Mana;
			player.RestoreMana(1000);
			Check(!player.TrySpendMana(1) && !player.TryUseSkill(skill) && player.Mana == manaAtDeath,
				"dead player cannot spend or restore mana");
			CombatTextSpawner.ShowMiss(player);
			Check(GetChildren().Count > 4, "combat text spawned in world");
			await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
			foreach (Node child in GetChildren()) Check(!child.Name.ToString().StartsWith("CombatText"), "floating text lifetime");
			GD.Print("PLAYER RESOURCES SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
