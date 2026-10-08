using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Skills;

namespace Zaomeng;

/// <summary>独立演员测试，不建立存档会话；验证共享行为、阵营和模板状态隔离。</summary>
public partial class MonsterTemplatesSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.Name = "Player";
			AddChild(player);
			player.InputEnabled = false;
			player.SetPhysicsProcess(false);
			var enemies = new Node2D { Name = "Enemies" };
			AddChild(enemies);
			var normal = Load("gorilla");
			var boss = Load("gorilla_boss");
			Check(normal.ActorScene is not null && boss.Health > normal.Health && boss.Attack > normal.Attack,
				"同种大猩猩的普通与Boss属性独立");
			var hard = GD.Load<LevelDifficultyDefinition>("res://Content/Levels/Difficulties/hard.tres");
			var pool = new MonsterPool(enemies, hard);
			var monster = pool.Spawn(normal, new(500, 100));
			monster.AiEnabled = false;
			monster.SetPhysicsProcess(false);
			float health = monster.MaxHealth;
			pool.Release(normal, monster);
			monster = pool.Spawn(normal, new(500, 100));
			monster.AiEnabled = false;
			monster.SetPhysicsProcess(false);
			Check(monster.MaxHealth == health && health == normal.Health * hard.MonsterHealthMultiplier,
				"困难模板复用不累计倍率");
			var random = new RandomNumberGenerator { Seed = 42 };
			Check(boss.Drops is null && normal.Drops!.Entries.All(e => e.ItemId.StartsWith("pt")),
				"旧版大猩猩Boss不掉装备，小怪仅掉普通装备");
			var monkey = Load("monkey");
			Check(Enumerable.Range(0, 1000).All(_ => monkey.Drops!.Roll(random).Count() <= 1), "旧小怪一次最多抽取一件装备");
			var bad = (MonsterDefinition)normal.Duplicate(true);
			bad.Skills[0].Skill.Animation = "missing_animation";
			bool rejected = false;
			try { bad.Validate(); } catch (InvalidOperationException) { rejected = true; }
			Check(rejected, "未提供技能动画的模板提前拒绝");

			var behavior = new HealSkillBehavior { Delay = 0.1f, HealthRatio = 0.2f, FlatHealingPerLevel = 0, MissingHealthMultiplier = 0 };
			var scene = new PackedScene();
			Check(scene.Pack(behavior) == Error.Ok, "共享行为场景装配");
			behavior.Free();
			var shared = new SkillDefinition { Id = "shared_heal", Animation = "hit1", BehaviorScene = scene };
			player.ResetForSpawn(new(200, 100));
			monster.ResetForSpawn(new(500, 100));
			player.ReceiveHit(new HitResult(30, Vector2.Zero, 0));
			monster.ReceiveHit(new HitResult(30, Vector2.Zero, 0));
			// 手动一步仅退出受击状态，之后动作与行为由真实动画时钟执行。
			player._PhysicsProcess(0.01);
			monster._PhysicsProcess(0.01);
			float playerBefore = player.Health, monsterBefore = monster.Health;
			Check(player.TryUseSkill(shared) && monster.TryUseSkill(shared), "玩家与怪物施放同一SkillDefinition和SkillBehavior");
			await Delay(0.25);
			Check(player.Health > playerBefore && monster.Health > monsterBefore, "共享治疗分别作用于施放者");
			monster.ResetForSpawn(new(500, 100));
			shared.CooldownSeconds = 0.5f;
			shared.StartCooldownOnImpact = true;
			Check(monster.TryUseSkill(shared), "怪物支持延迟启动冷却的共享动作");
			await Delay(0.9);
			Check(!monster.TryUseSkill(shared), "动作完成后等待行为通知的冷却仍阻止重施放");
			monster.StartPendingSkillCooldown(shared);
			Check(!monster.TryUseSkill(shared), "行为通知启动冷却");
			monster._PhysicsProcess(0.6);
			Check(monster.TryUseSkill(shared), "怪物冷却独立推进后可再次施放");
			shared.StartCooldownOnImpact = false;
			shared.CooldownSeconds = 0;
			player.ResetForSpawn(new(650, 100));
			monster.ResetForSpawn(new(500, 100));
			monster.Face(1);
			var projectile = GD.Load<PackedScene>("res://Content/Skills/Tangseng/IceDragonProjectile.tscn").Instantiate<SkillProjectile>();
			projectile.Configure(monster);
			AddChild(projectile);
			projectile.GlobalPosition = monster.GlobalPosition + new Vector2(0, -45);
			float targetBefore = player.Health;
			await Delay(0.5);
			Check(player.Health < targetBefore, "共享弹体由怪物施放时命中玩家阵营");
			var previousCast = GD.Load<PackedScene>("res://Content/Skills/Tangseng/IceDragonProjectile.tscn").Instantiate<SkillProjectile>();
			previousCast.Configure(monster);
			AddChild(previousCast);
			monster.ResetForSpawn(new(500, 100));
			await Delay(0.1);
			Check(!IsInstanceValid(previousCast), "对象池新出生不继承上次施放的弹体");
			Check(monster.TryUseSkill(shared), "怪物再次施放独立行为");
			monster.ReceiveHit(new HitResult(1, Vector2.Zero, 0.1f));
			float interrupted = monster.Health;
			await Delay(0.3);
			Check(monster.Health == interrupted && !monster.GetChildren().OfType<SkillBehavior>().Any(), "受击打断立即清理且不补发治疗");
			GD.Print("MONSTER TEMPLATES SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static MonsterDefinition Load(string id) => GD.Load<MonsterDefinition>($"res://Content/Monsters/Templates/{id}.tres");
	private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
