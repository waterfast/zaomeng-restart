using Godot;
using System;
using System.Threading.Tasks;

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
			Check(monster.NormalCombo.Count > 0, "monster normal combo is configured");
			monster.AiEnabled = false;
			player.InputEnabled = false;
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
			Check(monster.Health == monster.MaxHealth - 12, "one hit per swing including pre-existing overlaps");
			Check(sawKnockback && monster.Position.X > initialX, "knockback survives movement updates");
			Check(player.State == ActorState.Free && !player.AttackBox.Active, "attack end closes hitbox");
			Check(player.TryAttack(), "second attack starts");
			// hit2 opens its hitbox at 0.067 s; allow that key and a physics overlap check.
			await Frames(8);
			Check(monster.Health == monster.MaxHealth - 24, "next swing can damage same target");
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
			Check(monster.Health == health - 12, "left-facing attack");

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
				Hit = new HitDefinition { Damage = 5, Knockback = Vector2.Zero, Hitstun = 0 },
				HitStartFrame = 4,
				HitEndFrame = 6,
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
				Hit = new HitDefinition { Damage = 3, Knockback = Vector2.Zero, Hitstun = 0 },
				HitStartFrame = 4,
				HitEndFrame = 6
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
		// 技能先只配动画和 Hit 时，默认的 -1 帧不应阻止动画预览，也不能误伤目标。
		player.EquippedSkill5 = new SkillDefinition
		{
			Animation = "hit1",
			Hit = new HitDefinition { Damage = 20 }
		};
		monster.Position = player.Position - new Vector2(70, 0);
		health = monster.Health;
		Check(player.TryUseSkill(player.EquippedSkill5), "skill animation plays before hit frames are configured");
		await Frames(8);
		Check(player.Animator.CurrentAnimation == "hit1" && !player.AttackBox.Active
			&& monster.Health == health, "unconfigured hit frames do not cause damage");
		await Frames(22);

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
