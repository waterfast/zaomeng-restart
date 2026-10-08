using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Skills;

namespace Zaomeng;

public partial class EarlyBossSkillsSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.Name = "Player";
			player.MaxHealth = 100000;
			AddChild(player);
			player.InputEnabled = false;
			player.SetPhysicsProcess(false);
			var enemies = new Node2D { Name = "Enemies" };
			AddChild(enemies);
			var pool = new MonsterPool(enemies);
			foreach (string id in new[] { "macaque_king", "tamarin_king" })
			{
				var template = GD.Load<MonsterDefinition>($"res://Content/Monsters/Templates/{id}.tres");
				template.Validate();
				Check(template.IsBoss && template.Drops!.ChooseOne && template.Drops.RollProbability == 0.5f, "新Boss独立模板和掉落");
				var boss = pool.Spawn(template, new(500, 100));
				boss.AiEnabled = false;
				boss.SetPhysicsProcess(false);
				boss.Face(1);
				player.ResetForSpawn(new(650, 100));
				float before = player.Health;
				Check(boss.TryUseSkill(template.Skills[0].Skill), "Boss施放已登记的远程技能");
				for (int frame = 0; frame < 90; frame++)
				{
					await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
					boss._PhysicsProcess(0);
				}
				Check(player.Health < before, $"{id} 风弹/地面打击实际命中玩家");
				foreach (var choice in template.Skills)
				{
					boss.ResetForSpawn(new(500, 100));
					boss.Face(1);
					player.ResetForSpawn(new(choice.Skill.Hits.Count > 1 ? 555 : 600, 100));
					Check(boss.TryUseSkill(choice.Skill), $"{choice.Skill.Id} 可施放");
					int frames = (int)(boss.Animator.GetAnimation(choice.Skill.Animation).Length * 60) + 100;
					for (int frame = 0; frame < frames; frame++)
					{
						await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
						boss._PhysicsProcess(0);
					}
					Check(player.Health < player.MaxHealth, $"{choice.Skill.Id} 命中窗口/范围生效");
				}
				pool.Release(template, boss);
			}
			var tamarin = GD.Load<MonsterDefinition>("res://Content/Monsters/Templates/tamarin_king.tres");
			var caster = pool.Spawn(tamarin, new(500, 100));
			caster.AiEnabled = false;
			caster.SetPhysicsProcess(false);
			var ally = pool.Spawn(GD.Load<MonsterDefinition>("res://Content/Monsters/Templates/monkey.tres"), new(650, 100));
			ally.AiEnabled = false;
			ally.SetPhysicsProcess(false);
			player.ResetForSpawn(new(650, 100));
			float health = player.Health;
			Check(caster.TryUseSkill(tamarin.Skills[0].Skill), "预警施放");
			await Wait(0.2);
			player.Position = new(1100, 100);
			await Wait(2.6);
			Check(player.Health == health && ally.Health == ally.MaxHealth, "锁定脚点后可躲开，范围攻击不伤友军");
			caster.ResetForSpawn(new(500, 100));
			player.ResetForSpawn(new(650, 100));
			Check(caster.TryUseSkill(tamarin.Skills[0].Skill), "死亡取消测试施放");
			caster.ReceiveHit(new HitResult(10000, Vector2.Zero, 0, DamageType.True));
			await Wait(2.7);
			Check(player.Health == player.MaxHealth, "施法者死亡后未释放的地面打击取消");
			caster.ResetForSpawn(new(500, 100));
			player.ResetForSpawn(new(650, 100));
			Check(caster.TryUseSkill(tamarin.Skills[0].Skill), "已释放爆发死亡检查施放");
			await Wait(1.1);
			Check(enemies.GetChildren().Any(node => node is AreaAttackEffect), "范围爆发已释放到世界");
			caster.ReceiveHit(new HitResult(10000, Vector2.Zero, 0, DamageType.True));
			await Wait(1.6);
			Check(player.Health == player.MaxHealth, "死亡取消已释放爆发的后续命中");
			caster.ResetForSpawn(new(500, 100));
			Check(caster.TryUseSkill(tamarin.Skills[0].Skill), "已释放爆发复用检查施放");
			await Wait(1.1);
			caster.ResetForSpawn(new(500, 100));
			await Wait(1.6);
			Check(player.Health == player.MaxHealth, "新出生不继承上次释放的地面爆发");

			// 同一范围行为换成双方已有的 hit1 动作，证明它不依赖 Boss 类型和关卡。
			var behavior = tamarin.Skills[0].Skill.BehaviorScene!.Instantiate<AreaAttackBehavior>();
			behavior.Delay = 0.1f;
			behavior.Hits = new() { new HitEvent { StartFrame = 0, EndFrame = 2, Hit = behavior.Hits[0].Hit } };
			var scene = new PackedScene();
			Check(scene.Pack(behavior) == Error.Ok, "共用技能行为装配");
			behavior.Free();
			var shared = new SkillDefinition { Id = "shared_area", Animation = "hit1", FramesPerSecond = 60, BehaviorScene = scene };
			caster.ResetForSpawn(new(500, 100));
			player.ResetForSpawn(new(650, 100));
			Check(player.TryUseSkill(shared), "玩家施放同一范围行为");
			await Wait(0.3);
			Check(caster.Health < caster.MaxHealth, "玩家范围行为命中怪物阵营");
			GD.Print("EARLY BOSS SKILLS SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private static void Check(bool condition, string message)
	{ if (!condition) throw new InvalidOperationException(message); }
}
