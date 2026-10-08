using System;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Combat.Buffs;
using Zaomeng.Level;
using Zaomeng.Level.Traps;
using Zaomeng.Monsters;

namespace Zaomeng;

public partial class BuffTrapMechanicsSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			var floor = new StaticBody2D { CollisionLayer = 1, Position = new(0, 420) };
			floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new(2000, 40) } });
			AddChild(floor);
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.MaxHealth = 10000; player.MaxMana = 1000; player.Attack = 100; player.Gravity = 0;
			AddChild(player); player.InputEnabled = false; player.Position = new(100, 400);
			var pool = new MonsterPool(this);
			var kind = GD.Load<MonsterDefinition>("res://Content/Monsters/Human/monster_9.tres");
			var enemy = pool.Spawn(kind, new(160, 400));
			enemy.AiEnabled = false; enemy.SetPhysicsProcess(false);
			enemy.MaxHealth = 10000; enemy.Heal(10000); enemy.DodgeRating = player.CriticalRating = 0;
			int hits = 0; enemy.HitReceived += _ => hits++;
			var firestorm = (SkillDefinition)GD.Load<SkillDefinition>("res://Content/Skills/Wukong/lyfb.tres").Duplicate();
			firestorm.CooldownSeconds = 0;
			Check(player.TryUseSkill(firestorm), "烈焰风暴能起招");
			await Wait(0.7);
			Check(hits == 4, $"烈焰风暴真实动画应命中四次，实际 {hits}");
			Check(player.Buffs.Active.Count == 0, "施法结束清理范围 Buff");
			player.ResetForSpawn(new(100, 400)); hits = 0;
			Check(player.TryUseSkill(firestorm), "重生后重新施法");
			await Wait(0.08);
			player.ReceiveHit(new(1, Vector2.Zero, 0.1f, DamageType.True));
			int interrupted = hits; await Wait(0.4);
			Check(hits == interrupted && player.Buffs.Active.Count == 0, "受击打断不遗留风暴伤害");
			firestorm.Dispose();
			player.SetPhysicsProcess(false); player.ResetForSpawn(new(500, 400));
			var trap = GD.Load<PackedScene>("res://Scenes/Maps/GroundSpike.tscn").Instantiate<GroundSpikeTrap>();
			trap.Definition = new TrapDefinition { Damage = 50, RestSeconds = 0.2f, WarningSeconds = 0.1f, ActiveSeconds = 0.2f };
			trap.Position = new(500, 400); AddChild(trap); trap.SetPhysicsProcess(false);
			trap._PhysicsProcess(0.01); await Wait(0.08);
			float health = player.Health;
			trap._PhysicsProcess(0.2); Check(!trap.IsActive && player.Health == health, "预警不伤害");
			trap._PhysicsProcess(0.1); Check(trap.IsActive && player.Health == health - 50, "实际矩形区域地刺命中");
			trap._PhysicsProcess(0.05); Check(player.Health == health - 50, "同一伸出不重复命中");
			trap._PhysicsProcess(0.15); trap._PhysicsProcess(0.3);
			Check(player.Health == health - 100, "下次伸出可重新命中");
			player.Position = new(900, 400); await Wait(0.08);
			trap._PhysicsProcess(0.21); trap._PhysicsProcess(0.31);
			Check(player.Health == health - 100, "区域外不受地刺伤害");
			var stoneKind = GD.Load<MonsterDefinition>("res://Content/Monsters/Human/monster_8.tres");
			var stone = pool.Spawn(stoneKind, new(700, 400)); stone.AiEnabled = false; stone.SetPhysicsProcess(false);
			var shield = stone.GetNode<StoneShieldMechanic>("ShieldMechanic"); shield.SetPhysicsProcess(false);
			Check(stone.ReceiveHit(new(100, Vector2.Zero, 0)) is null, "石像护盾挡伤害");
			player.Position = shield.WeakPoint + new Vector2(0, 35); shield._PhysicsProcess(0.01);
			Check(!shield.Shielded && stone.ReceiveHit(new(100, Vector2.Zero, 0)) is not null, "触碰弱点解除护盾");
			shield._PhysicsProcess(5); Check(shield.Shielded, "五秒后护盾恢复");
			pool.Release(stoneKind, stone); stone = pool.Spawn(stoneKind, new(700, 400));
			Check(stone.GetNode<StoneShieldMechanic>("ShieldMechanic").Shielded, "池复用恢复机制");
			var birdKind = GD.Load<MonsterDefinition>("res://Content/Monsters/Human/monster_12.tres");
			var bird = pool.Spawn(birdKind, new(700, 400)); bird.AiEnabled = false; bird.SetPhysicsProcess(false);
			var flight = bird.GetNode<FlightPhaseMechanic>("FlightPhase"); flight.SetPhysicsProcess(false);
			flight._PhysicsProcess(10); Check(flight.Flying && bird.Gravity == 0, "鹏魔王进入飞行模式");
			var egg = bird.GetNode<RebirthMechanic>("Rebirth"); egg.SetPhysicsProcess(false);
			bird.ReceiveHit(new(bird.Health - bird.MaxHealth * 0.19f, Vector2.Zero, 0, DamageType.True));
			egg._PhysicsProcess(0.01); Check(egg.IsEgg && bird.Buffs.PreventsActions, "低血化卵禁行动");
			flight._PhysicsProcess(0.01); Check(!flight.Flying, "化卵结束飞行");
			float originalHealth = bird.MaxHealth; egg._PhysicsProcess(7);
			Check(bird.MaxHealth == originalHealth * 1.5f && bird.Health == bird.MaxHealth, "化卵七秒复生");
			pool.Release(birdKind, bird); bird = pool.Spawn(birdKind, new(700, 400));
			Check(bird.MaxHealth == originalHealth && !bird.GetNode<RebirthMechanic>("Rebirth").IsEgg, "复用不累计复生数值");
			bird.AiEnabled = false; bird.Gravity = 0; bird.Position = new(700, 250);
			bird.SetPhysicsProcess(true); bird.Face(-1); player.Position = new(450, 400);
			float beforeDive = player.Health;
			Check(bird.TryUseSkill(GD.Load<SkillDefinition>("res://Content/Monsters/Human/monster_12_bshn.tres")), "鹏魔王俯冲起招");
			await Wait(0.8);
			Check(player.Health < beforeDive, "俯冲真实移动并命中地面玩家");
			foreach (var voice in Audio.AudioManager.Instance!.GetChildren())
				if (voice is AudioStreamPlayer audio) { audio.Stop(); audio.Stream = null; }
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GD.Print("BUFF TRAP MECHANICS SMOKE TEST PASSED"); GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

