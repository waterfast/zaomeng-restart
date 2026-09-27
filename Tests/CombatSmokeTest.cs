using Godot;
using System;
using System.Threading.Tasks;
using Zaomeng.Character;

namespace Zaomeng;

/// <summary>Engine integration checks, run with -- --smoke-test.</summary>
public partial class CombatSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var arena = GetParent();
			var player = arena.GetNode<Player>("Player");
			var monster = arena.GetNode<Monster>("Monster");
			Check(player.NormalCombo.Count > 0, "player normal combo is configured");
			Check(player.NormalCombo.Count == 4 && player.NormalCombo[0].ResourcePath.Contains("Content/Attacks/Wukong"),
				"normal attacks load from their own content resources");
			Check(player.EquippedSkill1?.Hits.Count == 5 && player.EquippedSkill2?.Hits.Count == 4
				&& player.EquippedSkill3?.Hits.Count == 1 && player.EquippedSkill4?.Hits.Count == 1
				&& player.EquippedSkill5?.Hits.Count == 1,
				"equipped skills load their configured hit windows");
			Check(player.EquippedSkill2?.GetManaCost(1) == 35
				&& player.EquippedSkill2.GetManaCost(2) == 43
				&& Mathf.IsEqualApprox(player.EquippedSkill2.CooldownSeconds, 3.2f),
				"skill mana and cooldown data match the original formula");
			Check(monster.NormalCombo.Count > 0, "monster normal combo is configured");
			monster.AiEnabled = false;
			CheckStatRules(player, monster, ((TestArena)arena).ItemCatalog);
			player.InputEnabled = false;
			player.Attack = 12;
			player.CriticalRating = 0;
			player.PhysicalDefense = 0;
			monster.CriticalRating = 0;
			player.Position = new(400, 490);
			monster.Position = new(470, 490);
			await Frames(20);
			Check(player.IsOnFloor() && monster.IsOnFloor(), "original map floor collision");
			Check(Math.Abs(player.Animator.GetAnimation("hit1").Length - 0.35) < 0.001, "0.35 second attack timeline");
			Check(player.TryAttack(), "attack starts");
			float initialX = monster.Position.X;
			bool sawKnockback = false;
			for (int i = 0; i < 28; i++)
			{
				await Frames(1);
				sawKnockback |= monster.Motor.ExternalVelocityX > 0;
			}
			Check(monster.Health <= monster.MaxHealth - 12 && monster.Health >= monster.MaxHealth - 14.4f,
				"one hit per swing uses the first normal attack's old multiplier range");
			Check(sawKnockback && monster.Position.X > initialX, "knockback survives movement updates");
			Check(player.State == ActorState.Free && !player.AttackBox.Active, "attack end closes hitbox");
			Check(player.TryAttack(), "second attack starts");
			// hit2 opens its hitbox at 0.067 s; allow that key and a physics overlap check.
			await Frames(8);
			Check(monster.Health < monster.MaxHealth - 22 && monster.Health > monster.MaxHealth - 28,
				"next normal attack can damage the same target");
			player.ReceiveHit(new HitResult(1, new(-60, 0), 0.3f));
			Check(!player.AttackBox.Active && player.State == ActorState.Hurt, "hurt interrupts attack immediately");
			await Frames(30);
			Check(player.State == ActorState.Free && !player.AttackBox.Active, "interrupted timeline cannot reopen attack");
			monster.Team = player.Team;
			Check(!CombatResolver.Resolve(player, monster, player.NormalCombo[0].Hit), "friendly fire rejected");
			monster.Team = 1;
			monster.Position = player.Position + new Vector2(38, 0);
			monster.Face(-1);
			await Frames(3);
			float health = player.Health;
			Check(monster.TryAttack(), "monster attack starts");
			await Frames(30);
			Check(player.Health == health - 8, "monster timeline damages player");
			await Frames(30);
			player.InputEnabled = true;
			Input.ActionPress("jump");
			await Frames(2);
			Input.ActionRelease("jump");
			Check(player.Velocity.Y < 0, "player jumps from floor");
			Check(player.TryJump(), "second jump starts");
			Check(player.Animator.CurrentAnimation == "jump2", "second jump plays jump2");
			await Frames(5);
			Check(player.GetNode<Sprite2D>("Facing/Visual/RoleBody").Frame > 30,
				"jump2 advances body frames");
			await Frames(110);
			Check(player.IsOnFloor(), "player lands");
			player.InputEnabled = false;
			// Mirror the same attack to verify visuals and attack geometry face together.
			monster.Position = player.Position - new Vector2(70, 0);
			player.Face(-1);
			await Frames(3);
			health = monster.Health;
			player.TryAttack();
			await Frames(28);
			Check(monster.Health <= health - 12 && monster.Health >= health - 14.4f, "left-facing attack");

			// 用第 5 个输入动作验证槽位映射、命中帧和特效释放帧。
			monster.Position = player.Position - new Vector2(70, 0);
			var effectRoot = new Node2D { Name = "SkillEffectMarker" };
			var effectScene = new PackedScene();
			Check(effectScene.Pack(effectRoot) == Error.Ok, "skill effect scene packs");
			effectRoot.Free();
			var skill = new SkillDefinition
			{
				Animation = "hit1",
				FramesPerSecond = 30,
				Hits = new() { new HitEvent { StartFrame = 4, EndFrame = 6,
					Hit = new HitDefinition { AttackMultiplier = 0, AttackMultiplierMax = 0,
						FlatDamage = 5, Knockback = Vector2.Zero, Hitstun = 0 } } },
				EffectScene = effectScene,
				EffectFrame = 3,
				EffectLifetime = 1
			};
			player.EquippedSkill5 = skill;
			player.InputEnabled = true;
			health = monster.Health;
			Input.ActionPress("skill_5");
			await Frames(2);
			Input.ActionRelease("skill_5");
			Check(player.State == ActorState.Attacking, "fifth skill input starts the configured animation");
			await Frames(2);
			Check(monster.Health == health, "skill does not hit before its start frame");
			await Frames(10);
			Check(monster.Health == health - 5, "skill hit uses its configured frame and damage");
			Check(player.GetNode<Node2D>("Facing").HasNode("SkillEffectMarker"),
				"skill effect spawns at its configured frame");
			await Frames(20);
			Check(player.State == ActorState.Free && !player.AttackBox.Active && player.CurrentHit == null,
				"skill completion closes its hitbox");
			// 同一个快捷栏换上另一个技能后，skill_5 应释放新的配置。
			player.EquippedSkill5 = new SkillDefinition
			{
				Animation = "hit1",
				FramesPerSecond = 30,
				Hits = new() { new HitEvent { StartFrame = 4, EndFrame = 6,
					Hit = new HitDefinition { AttackMultiplier = 0, AttackMultiplierMax = 0,
						FlatDamage = 3, Knockback = Vector2.Zero, Hitstun = 0 } } }
			};
			monster.Position = player.Position - new Vector2(70, 0);
			health = monster.Health;
			Input.ActionPress("skill_5");
			await Frames(2);
			Input.ActionRelease("skill_5");
			await Frames(12);
			Check(monster.Health == health - 3, "same slot releases the newly equipped skill");
			player.InputEnabled = false;
		player.ReceiveHit(new HitResult(1, Vector2.Zero, 0.15f));
		Check(player.State == ActorState.Hurt && !player.AttackBox.Active && player.CurrentHit == null,
			"hurt interrupts the skill and closes its hitbox");
		await Frames(20);
		// 没有命中事件的技能允许预览动画，也不会误伤目标。
		player.EquippedSkill5 = new SkillDefinition
		{
			Animation = "hit1"
		};
		monster.Position = player.Position - new Vector2(70, 0);
		health = monster.Health;
		Check(player.TryUseSkill(player.EquippedSkill5), "skill animation plays without hit events");
		await Frames(8);
		Check(player.Animator.CurrentAnimation == "hit1" && !player.AttackBox.Active
			&& monster.Health == health, "unconfigured hit frames do not cause damage");
		await Frames(22);

		// 相邻两个窗口共享攻击框，但每段应能重新命中同一个已在范围内的目标。
		monster.ResetForSpawn(player.Position - new Vector2(70, 0));
		player.EquippedSkill5 = new SkillDefinition
		{
			Animation = "hit1",
			Hits = new()
			{
				new HitEvent { StartFrame = 1, EndFrame = 3,
					Hit = new HitDefinition { AttackMultiplier = 0, FlatDamage = 5,
						Knockback = Vector2.Zero, Hitstun = 0 } },
				new HitEvent { StartFrame = 4, EndFrame = 6,
					Hit = new HitDefinition { AttackMultiplier = 0, FlatDamage = 5,
						Knockback = Vector2.Zero, Hitstun = 0 } }
			}
		};
		health = monster.Health;
		Check(player.TryUseSkill(player.EquippedSkill5), "multi-window skill starts");
		await Frames(10);
		Check(monster.Health == health - 10, "each hit window can damage the same target once");
		await Frames(20);
		monster.ResetForSpawn(player.Position - new Vector2(70, 0));
		var leveledCharacter = new Zaomeng.Character.Character
		{
			BaseStats = new() { Attack = 12 }
		};
		leveledCharacter.LearnSkill("level_test", 2);
		player.BindCharacter(leveledCharacter, GetParent<TestArena>().ItemCatalog);
		var leveledSkill = new SkillDefinition
		{
			Id = "level_test",
			Animation = "hit1",
			Hits = new() { new HitEvent { StartFrame = 1, EndFrame = 3,
				Hit = new HitDefinition { AttackMultiplier = 1, MultiplierPerLevel = 0.5f,
					Knockback = Vector2.Zero, Hitstun = 0 } } }
		};
		health = monster.Health;
		Check(player.TryUseSkill(leveledSkill), "learned skill starts at its saved level");
		await Frames(6);
		Check(monster.Health == health - 18, "skill multiplier uses the saved level at hit time");
		await Frames(24);

		monster.ReceiveHit(new HitResult(1000, Vector2.Zero, 0));
			Check(monster.IsDead && !monster.TryAttack(), "death blocks attacks");
			Check(!CombatResolver.Resolve(player, monster, player.NormalCombo[0].Hit), "dead targets ignored");
			await Frames(10);
			await CheckActionMotion(player);
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://Tests/combat-preview.png");
			}
			GD.Print("COMBAT_SMOKE_TEST: PASS");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError($"COMBAT_SMOKE_TEST: FAIL {error}");
			GetTree().Quit(1);
		}
	}

	private static void CheckStatRules(Player player, Monster monster, Zaomeng.Items.ItemCatalog catalog)
	{
		var character = new Zaomeng.Character.Character { Id = "role_1", Level = 10 };
		Check(CharacterProgression.SyncBaseStats(character)
			&& character.BaseStats.MaxHealth == 530 && character.BaseStats.MaxMana == 185
			&& character.BaseStats.Attack == 44 && character.BaseStats.PhysicalDefense == 19,
			"old role growth uses linear level formulas");
		Check(CharacterProgression.ExperienceToNextLevel(19) == 4000
			&& CharacterProgression.ExperienceToNextLevel(20) == 10000,
			"old experience threshold switches from table to formula");
		character.PermanentBonuses.Attack = 2;
		character.Equipment.WeaponId = "qld";
		CharacterStats stats = CharacterStatCalculator.Calculate(character, catalog);
		Check(stats.Attack == 91 && stats.CriticalRating == 3 && stats.Accuracy == 5,
			"equipped weapon and permanent bonuses enter final player stats");
		using (var probe = new Player())
		{
			probe.BindCharacter(character, catalog);
			Check(probe.Level == 10 && probe.MaxHealth == 530 && probe.Attack == 91
				&& probe.CriticalRating == 3 && probe.Accuracy == 5,
				"player actor receives calculated level and equipment stats");
		}

		player.Level = 1;
		monster.Level = 1;
		player.Accuracy = 0;
		player.CriticalRating = 0;
		player.Luck = 0;
		player.ArmorPenetration = 0;
		monster.CriticalResistance = 0;
		monster.Toughness = 0;
		monster.PhysicalDefense = 100;
		Check(LegacyDamageCalculator.Calculate(player, monster, 20, DamageType.Physical,
			false, 1, 1).Damage == 10, "old monster defense reduction");
		monster.DodgeRating = 70;
		Check(LegacyDamageCalculator.Calculate(player, monster, 20, DamageType.Physical,
			false, 0.4f, 1).Missed, "old dodge versus accuracy roll");
		monster.DodgeRating = 0;
		player.CriticalRating = 100;
		player.Luck = 100;
		Check(LegacyDamageCalculator.Calculate(player, monster, 20, DamageType.Physical,
			true, 1, 0).Damage == 25, "old critical and luck multipliers");
		player.CriticalRating = 0;
		player.Luck = 0;
		player.ArmorPenetration = 100;
		Check(LegacyDamageCalculator.Calculate(player, monster, 20, DamageType.Physical,
			false, 1, 1).Damage == 20, "armor penetration removes effective defense");
		player.ArmorPenetration = 0;
		player.Level = 10;
		Check(LegacyDamageCalculator.Calculate(player, monster, 20, DamageType.Physical,
			false, 1, 1).Damage == 11, "old higher-level player damage bonus");
		player.Level = 1;
		monster.PhysicalDefense = 0;
	}

	private async Task CheckActionMotion(Player player)
	{
		player.InputEnabled = true;
		player.Position = new(400, 490);
		player.Velocity = Vector2.Zero;
		player.Face(1);
		Input.ActionPress("move_right");
		await Frames(3);
		Check(player.Velocity.X > 0, "player runs before attacking");
		Check(player.TryAttack(), "first normal attack starts with running momentum");
		Input.ActionRelease("move_right");
		player.InputEnabled = false;
		float startX = player.Position.X;
		await Frames(4);
		Check(player.Position.X > startX + 8 && player.Velocity.X > 0,
			"first normal attack carries entry momentum");
		player.ReceiveHit(new HitResult(0, Vector2.Zero, 0.1f));
		await Frames(1);
		Check(Mathf.IsZeroApprox(player.Velocity.X), "hurt cancels attack movement");
		await Frames(12);

		player.Position = new(400, 490);
		player.Velocity = Vector2.Zero;
		player.InputEnabled = true;
		Input.ActionPress("move_right");
		Input.ActionPress("jump");
		await Frames(2);
		Input.ActionRelease("jump");
		Check(player.Velocity.Y < 0 && player.Velocity.X > 0,
			"player moves horizontally while jumping");
		Check(player.TryAttack(), "airborne normal attack starts");
		Input.ActionRelease("move_right");
		player.InputEnabled = false;
		startX = player.Position.X;
		await Frames(4);
		Check(player.Position.X > startX + 8, "airborne normal attack carries momentum");
		player.ReceiveHit(new HitResult(0, Vector2.Zero, 0.1f));
		await Frames(12);

		player.Position = new(400, 490);
		player.Velocity = Vector2.Zero;
		player.Face(1);
		Check(player.TryUseSkill(player.EquippedSkill3), "fire dash starts facing right");
		startX = player.Position.X;
		await Frames(5);
		Check(player.Position.X > startX + 50, "fire dash moves the body right");
		player.Animator.Pause();
		startX = player.Position.X;
		await Frames(2);
		Check(Mathf.Abs(player.Position.X - startX) < 1,
			"pausing the animation pauses its dash");
		player.ReceiveHit(new HitResult(0, Vector2.Zero, 0.1f));
		await Frames(1);
		Check(Mathf.IsZeroApprox(player.Velocity.X), "hurt cancels fire dash");
		await Frames(12);

		player.Position = new(400, 490);
		player.Velocity = Vector2.Zero;
		player.Face(-1);
		Check(player.TryUseSkill(player.EquippedSkill3), "fire dash starts facing left");
		startX = player.Position.X;
		await Frames(5);
		Check(player.Position.X < startX - 50, "fire dash locks its starting direction");
		await Frames(30);
		Check(player.State == ActorState.Free && Mathf.IsZeroApprox(player.Velocity.X),
			"fire dash stops when its animation ends");

		// 烈焰风暴在地面停下，在空中保留起招前的水平速度。
		player.Position = new(400, 300);
		await Frames(2);
		player.Velocity = new(180, 0);
		Check(!player.IsOnFloor() && player.TryUseSkill(player.EquippedSkill2),
			"airborne fire storm starts");
		var fireStormEffect = player.GetNode<AnimatedSprite2D>("Facing/Visual/SpecialEffect");
		Check(fireStormEffect.Visible && fireStormEffect.Animation == "lyfb",
			"fire storm shows its body effect");
		startX = player.Position.X;
		await Frames(4);
		Check(player.Position.X > startX + 8, "airborne fire storm carries momentum");
		Check(fireStormEffect.Frame > 0, "fire storm advances effect frames");
		player.ReceiveHit(new HitResult(0, Vector2.Zero, 0.1f));
		Check(!fireStormEffect.Visible, "hurt hides the interrupted fire storm effect");
	}

	private async Task Frames(int count)
	{
		for (int i = 0; i < count; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	}

	private static void Check(bool condition, string description)
	{
		if (!condition) throw new InvalidOperationException(description);
		GD.Print($"PASS: {description}");
	}
}
