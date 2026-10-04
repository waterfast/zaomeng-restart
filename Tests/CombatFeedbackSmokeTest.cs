using System;
using System.Linq;
using Godot;
using Zaomeng.Character;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Save;

namespace Zaomeng;

/// <summary>使用独立角色和钱包验证原版飘字、光球奖励与小怪回收，不访问存档。</summary>
public partial class CombatFeedbackSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			if (OS.GetCmdlineUserArgs().Contains("--feedback-preview"))
			{
				await CapturePreview();
				GetTree().Quit();
				return;
			}
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			var character = new Zaomeng.Character.Character { Id = "role_1", Name = "悟空" };
			CharacterProgression.SyncBaseStats(character);
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.BindCharacter(character, catalog);
			player.InputEnabled = false;
			AddChild(player);
			player.Position = new Vector2(400, 300);
			player.SetPhysicsProcess(false);
			var wallet = new Wallet();
			int collections = 0;
			player.SoulsCollected += amount => { wallet.Add(CurrencyType.Soul, amount); collections++; };
			Check(!player.HasNode("HealthBar"), "player overhead health bar removed");
			CheckDigits(DamageType.Physical, false, false, "monster/physics/physics", "physics");
			CheckDigits(DamageType.Magic, false, false, "magic/magic", "physics");
			CheckDigits(DamageType.Physical, true, false, "physicscrit/physics", "physics");
			CheckDigits(DamageType.Magic, true, false, "magiccrit/magic", "physics");
			CheckDigits(DamageType.Magic, true, true, "magic/magic", "Crit");
			var pool = new MonsterPool(this);
			int[] experiences = [1, 5, 10];
			int[] soulsPerOrb = [1, 2, 2];
			for (int kind = 1; kind <= 3; kind++)
			{
				Monster monster = pool.Spawn(kind, player.GlobalPosition + new Vector2(60, 0));
				monster.AiEnabled = false;
				monster.SetPhysicsProcess(false);
				Check(!monster.HasNode("HealthBar"), $"monster {kind} overhead health bar removed");
				Check(monster.ExperienceReward == experiences[kind - 1] && monster.SoulValuePerOrb == soulsPerOrb[kind - 1],
					$"monster {kind} original rewards");
				var body = monster.GetNode<AnimatedSprite2D>("Facing/Visual/Body");
				foreach (StringName animation in body.SpriteFrames.GetAnimationNames())
					for (int frame = 0; frame < body.SpriteFrames.GetFrameCount(animation); frame++)
						Check(body.SpriteFrames.GetFrameTexture(animation, frame).GetImage().GetUsedRect().Size != Vector2I.Zero,
							$"monster {kind} {animation} frame {frame} has pixels");
				long experienceBefore = player.Experience;
				long soulsBefore = wallet.Souls;
				int collectionsBefore = collections;
				var lethal = new HitDefinition { DamageType = DamageType.True, FlatDamage = 10000, CanCrit = false };
				Check(CombatResolver.Resolve(player, monster, lethal) && player.Experience == experienceBefore + experiences[kind - 1],
					$"monster {kind} experience on death");
				Check(GetChildren().OfType<SoulPickup>().Count() == 3 && wallet.Souls == soulsBefore,
					"three original soul orbs spawn without immediate currency credit");
				Check(!CombatResolver.Resolve(player, monster, lethal) && GetChildren().OfType<SoulPickup>().Count() == 3,
					"dead monster cannot duplicate rewards");
				monster.Animator.Seek(0.5, update: true);
				Check(body.SelfModulate.A == 0, "death animation fades monster body");
				pool.Release(kind, monster);
				Monster reused = pool.Spawn(kind, player.GlobalPosition + new Vector2(150, 0));
				reused.AiEnabled = false;
				reused.SetPhysicsProcess(false);
				Check(ReferenceEquals(monster, reused) && body.SelfModulate.A == 1,
					"pooled monster restores body visibility");
				await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
				Check(wallet.Souls == soulsBefore + 3 * soulsPerOrb[kind - 1] && collections == collectionsBefore + 3,
					$"monster {kind} soul orbs collect once after homing");
				Check(!GetChildren().OfType<SoulPickup>().Any(), "collected soul orbs are freed");
				pool.Release(kind, reused);
			}
			GD.Print("COMBAT FEEDBACK SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private async System.Threading.Tasks.Task CapturePreview()
	{
		var level = GD.Load<PackedScene>("res://Scenes/Level/Level_1.tscn").Instantiate<GameplayLevel>();
		AddChild(level);
		level.SpawnInterval = 100;
		var player = level.GetNode<Player>("Player");
		player.InputEnabled = false;
		await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
		var hits = new HitResult[]
		{
			new(123, Vector2.Zero, 0, DamageType.Physical),
			new(456, Vector2.Zero, 0, DamageType.Magic),
			new(789, Vector2.Zero, 0, DamageType.Physical, true),
			new(987, Vector2.Zero, 0, DamageType.Magic, true)
		};
		for (int i = 0; i < hits.Length; i++)
		{
			var monster = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			level.GetNode("Enemies").AddChild(monster);
			monster.AiEnabled = false;
			monster.SetPhysicsProcess(false);
			monster.GlobalPosition = player.GlobalPosition + new Vector2(75 + i * 130, 0);
			CombatTextSpawner.ShowDamage(monster, hits[i]);
		}
		await ToSignal(GetTree().CreateTimer(0.22), SceneTreeTimer.SignalName.Timeout);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng("res://.godot/legacy-combat-feedback-preview.png");
	}

	private void CheckDigits(DamageType type, bool critical, bool playerWasHit, string path, string animation)
	{
		var effect = GD.Load<PackedScene>("res://Scenes/Effects/DamageText.tscn").Instantiate<LegacyDamageText>();
		effect.Configure(new HitResult(123, Vector2.Zero, 0, type, critical), playerWasHit);
		AddChild(effect);
		var digits = effect.GetNode<HBoxContainer>("Midlle/Number");
		Check(digits.GetChildCount() == 3, "damage composed from original digit textures");
		for (int index = 0; index < 3; index++)
			Check(digits.GetChild<TextureRect>(index).Texture.ResourcePath == $"res://Assets/Art/AllNumber/{path}_{index + 1}.png",
				$"{type} critical={critical} digit {index + 1} uses correct original art");
		var animator = effect.GetNode<AnimationPlayer>("ShowPlayer");
		Check(animator.CurrentAnimation == animation, "original target-specific animation selected");
		animator.Seek(0.1, update: true);
		if (animation == "physics")
			Check(effect.GetNode<Node2D>("Midlle").Scale == Vector2.One * 2, "original 0.1 second scale peak");
		animator.Seek(0.6, update: true);
		Check(effect.GetNode<Node2D>("Midlle").Position == new Vector2(0, -50), "original rise trajectory");
		effect.QueueFree();
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		if (!message.Contains("has pixels")) GD.Print($"PASS: {message}");
	}
}
