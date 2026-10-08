using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Save;
using Zaomeng.Skills;

namespace Zaomeng;

public partial class DragonPalaceSmokeTest : Node
{
	public override void _Ready() => GetTree().Root.CallDeferred(Node.MethodName.AddChild, new DragonPalaceTestRunner());
}

public partial class DragonPalaceTestRunner : Node
{
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private static void Check(bool condition, string message)
	{ if (!condition) throw new InvalidOperationException(message); }
	public override async void _Ready()
	{
		try
		{
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			foreach (string tier in OS.GetCmdlineUserArgs().Contains("--entrance-only") ? Array.Empty<string>() : new[] { "low", "high" })
			{
				var definition = GD.Load<LevelDefinition>($"res://Content/Levels/crystal_palace_{tier}.tres");
				definition.Validate(catalog);
				Check(!definition.AdvancesCampaign && definition.ProgressLevel == 2, "隐藏关不推进主线");
				var boss = definition.WaveEncounter!.Waves[0].Monsters[0];
				string[] expected = tier == "low" ? ["qld", "bhz"]
					: ["ryjgb", "jhcz", "qxsh"];
				Check(boss.Drops!.ChooseOne && boss.Drops.RollProbability == .4f &&
					boss.Drops.PossibleItemIds.Order().SequenceEqual(expected.Order()), "原版完整掉落表");
				var random = new RandomNumberGenerator { Seed = 66 };
				int dropped = 0;
				for (int i = 0; i < 1000; i++)
				{
					var rolls = boss.Drops.Roll(random).ToArray();
					Check(rolls.Length <= 1 && rolls.All(r => expected.Contains(r.ItemId)), "只抽取已注册的一件装备");
					dropped += rolls.Length;
				}
				Check(dropped is > 320 and < 480, "40%抽样掉率");
			}
			if (!OS.GetCmdlineUserArgs().Contains("--entrance-only")) await CheckSkills();
			await CheckEntrance("low", 20, 1950);
			await CheckEntrance("high", 21, 3300);
			GD.Print("DRAGON PALACE SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}

	private async Task CheckSkills()
	{
		var arena = new Node2D(); GetTree().Root.AddChild(arena);
		arena.AddChild(GD.Load<PackedScene>("res://Scenes/Maps/CrystalPalace.tscn").Instantiate());
		var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
		player.Name = "Player"; player.MaxHealth = 100000;
		arena.AddChild(player); player.InputEnabled = false; player.SetPhysicsProcess(false);
		var enemies = new Node2D { Name = "Enemies" }; arena.AddChild(enemies);
		var pool = new MonsterPool(enemies);
		foreach (string tier in new[] { "low", "high" })
		{
			var template = GD.Load<MonsterDefinition>($"res://Content/Monsters/DragonKing/{tier}.tres");
			var boss = pool.Spawn(template, new(500, 478));
			boss.AiEnabled = false; boss.SetPhysicsProcess(false);
			var ally = pool.Spawn(template, new(500, 478));
			ally.AiEnabled = false; ally.SetPhysicsProcess(false);
			foreach (int direction in new[] { 1, -1 })
				foreach (var choice in template.Skills)
				{
					boss.ResetForSpawn(new(500, 478)); boss.Face(direction);
					float distance = choice.Skill.Animation == "qnbx" && tier == "low" ? 182 : 110;
					player.ResetForSpawn(new(500 + direction * distance, 478));
					float health = player.Health;
					int released = 0;
					void CountEffect(Node node) { if (node is AreaAttackEffect) released++; }
					enemies.ChildEnteredTree += CountEffect;
					Check(boss.TryUseSkill(choice.Skill), $"{choice.Skill.Id} 可施放");
					Check(!boss.TryUseSkill(choice.Skill), "动作期间拒绝重复施放");
					double duration = choice.Skill.Animation == "qnhb" ? 3.2 : 2.3;
					if (OS.GetCmdlineUserArgs().Contains("--capture-dragon"))
					{
						double sample = choice.Skill.Animation.ToString() switch { "qnhb" => 2.35, "qnbx" => 1.9, "htsl" => 1.1, _ => .28 };
						await Wait(sample); await Capture($"{choice.Skill.Id}-{direction}");
						await Wait(duration - sample);
					}
					else await Wait(duration);
					enemies.ChildEnteredTree -= CountEffect;
					int expectedEffects = choice.Skill.Animation.ToString() switch { "qnbx" => 5, "qnhb" => 9, "htsl" => 1, _ => 0 };
					Check(released == expectedEffects, $"{choice.Skill.Id} 完整释放 {released}/{expectedEffects}");
					Check(player.Health < health, $"{choice.Skill.Id} 朝向{direction}实际命中");
					Check(ally.Health == ally.MaxHealth, "范围不伤友军");
					if (choice.Skill.Animation == "qnbx")
						Check(player.Buffs.PreventsActions && player.Buffs.Active.Any(b => b.Definition.Duration == (tier == "low" ? 4 : 5)), "冻结时长与动作限制");
					if (choice.Skill.Animation == "qnhb" && tier == "high")
					{
						var bleed = player.Buffs.Active.Single(b => b.Definition.Id == "dragon_bleed").Definition;
						float before = player.Health;
						player.Buffs.Tick(.6f);
						Check(bleed.Duration == 5 && bleed.PulseInterval == .5f && player.Health < before, "高阶流血脉冲");
					}
					if (choice.Skill.Animation != "hit1")
					{
						boss._PhysicsProcess(0);
						Check(!boss.TryUseSkill(choice.Skill), "技能冷却阻止连续施放");
					}
					GD.Print($"PASS {choice.Skill.Id} direction={direction}");
				}
			// 高阶冰柱起招锁定位置，移动躲开；死亡和新出生不能继续释放。
			var ice = template.Skills.Single(c => c.Skill.Animation == "qnbx").Skill;
			boss.ResetForSpawn(new(500, 478)); player.ResetForSpawn(new(650, 478));
			Check(boss.TryUseSkill(ice), "冰柱取消检查起招");
			await Wait(.2);
			boss.ReceiveHit(new HitResult(100000, Vector2.Zero, .2f, DamageType.True));
			await Wait(2);
			Check(player.Health == player.MaxHealth, "死亡取消延迟冰柱");
			boss.ResetForSpawn(new(500, 478)); player.ResetForSpawn(new(650, 478));
			Check(boss.TryUseSkill(ice), "冰柱复用检查起招");
			await Wait(.2); boss.ResetForSpawn(new(500, 478)); await Wait(2);
			Check(player.Health == player.MaxHealth, "池复用取消旧序列");
			boss.ResetForSpawn(new(500, 478)); player.ResetForSpawn(new(650, 478));
			Check(boss.TryUseSkill(ice), "冰柱打断检查起招");
			await Wait(.2); boss.ReceiveHit(new HitResult(1, Vector2.Zero, .4f, DamageType.True)); await Wait(2);
			Check(player.Health == player.MaxHealth, "受击取消未释放冰柱");
			if (tier == "high")
			{
				boss.ResetForSpawn(new(500, 478)); player.ResetForSpawn(new(650, 478));
				Check(boss.TryUseSkill(ice), "锁定脚点检查起招");
				await Wait(.3); player.Position = new(1200, 478); await Wait(2);
				Check(player.Health == player.MaxHealth, "高阶冰柱起招锁定后可躲开");
			}
			pool.Release(template, ally); pool.Release(template, boss);
		}
		arena.QueueFree(); await Wait(.1);
	}

	private async Task CheckEntrance(string tier, int characterLevel, float riverX)
	{
		GameSession.SelectNewSlot(0);
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		var (character, inventory) = GameSessionCharacter.Prepare(catalog);
		character.Level = characterLevel;
		Zaomeng.Character.CharacterProgression.SyncBaseStats(character);
		var water = GD.Load<PackedScene>("res://Scenes/Level/Level_2.tscn").Instantiate<GameplayLevel>();
		GetTree().Root.AddChild(water);
		GetTree().CurrentScene = water;
		water.GetChildren().OfType<WaveEncounter>().Single().SetPhysicsProcess(false);
		foreach (Node gate in water.GetNode("WaveGates").GetChildren())
			if (gate is CollisionShape2D shape) shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		var player = water.GetNode<Player>("Player");
		var entrance = water.GetNode<HiddenWaterEntrance>("Map/HiddenWaterEntrance");
		Check(entrance.DestinationFor(characterLevel).Id == $"crystal_palace_{tier}", "21级门槛");
		await Wait(.2);
		Check(!entrance.TryBeginDive(), "岸上不能下潜");
		player.ResetForSpawn(new(riverX, 460));
		Check(!entrance.TryBeginDive(), "空中不能下潜");
		await Wait(.7);
		Check(player.IsOnFloor() && Mathf.Abs(player.Position.Y - 560) < 3, $"水面站立 {player.Position}");
		Input.ActionPress("jump"); await Wait(.04); Input.ActionRelease("jump");
		Check(!entrance.IsDiving && player.Velocity.Y < 0, "单独K仍普通跳跃");
		player.ResetForSpawn(new(riverX, 556)); await Wait(.15);
		player.RestoreEntranceVitals(player.MaxHealth * .7f, player.MaxMana * .6f);
		float health = player.Health, mana = player.Mana;
		Input.ActionPress("move_down"); Input.ActionPress("jump"); await Wait(.04);
		Input.ActionRelease("jump"); Input.ActionRelease("move_down");
		Check(entrance.IsDiving && !player.InputEnabled, $"S+K实际下潜且禁用跳跃 dive={entrance.IsDiving} input={player.InputEnabled} velocity={player.Velocity} floor={player.IsOnFloor()} pos={player.Position} state={player.State}");
		await Wait(.08);
		Check(player.Position.Y > 560 && GameSession.SelectedLevel is null, $"先落水再切关 pos={player.Position} vel={player.Velocity} selected={GameSession.SelectedLevel?.Id}");
		await Wait(3.5);
		var hidden = GetTree().CurrentScene as GameplayLevel;
		Check(hidden is not null && GameSession.SelectedLevel!.Id == $"crystal_palace_{tier}", "真实隐藏关切换");
		if (IsInstanceValid(water)) water.QueueFree();
		var hiddenPlayer = hidden!.GetNode<Player>("Player");
		Check(hiddenPlayer.Health <= health + 1 && hiddenPlayer.Mana <= mana + 1 && hiddenPlayer.IsOnFloor(), "切关保留体力法力，出生脚点可站立");
		Check(hidden.GetChildren().OfType<WaveEncounter>().Single().Boss?.Definition?.Id == $"dragon_king_{tier}", "正确龙王生成");
		Check(GameSession.TakeEntranceVitals($"crystal_palace_{tier}") is null, "快照只消费一次");
		if (OS.GetCmdlineUserArgs().Contains("--capture-dragon")) await Capture($"palace-{tier}");
		var boss = hidden.GetChildren().OfType<WaveEncounter>().Single().Boss!;
		boss.AiEnabled = false;
		float dropChance = boss.Definition!.Drops!.RollProbability;
		boss.Definition.Drops.RollProbability = 1;
		boss.ReceiveHit(new HitResult(100000, Vector2.Zero, 0, DamageType.True));
		await Wait(.3);
		boss.Definition.Drops.RollProbability = dropChance;
		var loot = hidden.GetChildren().OfType<ItemPickup>().Single();
		string itemId = loot.Item.Id;
		int before = inventory.GetItemCount(itemId);
		await Wait(.7);
		hiddenPlayer.Position = loot.Position;
		await Wait(.2);
		Check(inventory.GetItemCount(itemId) == before + 1, "击败龙王后装备落地可拾取");
		await Wait(1.3);
		var exit = hidden.GetChildren().OfType<LevelExit>().Single();
		hiddenPlayer.Position = exit.Position;
		await Wait(.1);
		exit._Input(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
		await Wait(.2);
		Check(hidden.HasNode("Settlement") && hidden.GetNode<Zaomeng.UI.LevelSettlement>("Settlement").Result.Victory
			&& GameSession.Data!.UnlockedLevel == 1, "隐藏关胜利结算不解锁主线");
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(hidden.SceneFilePath); await Wait(.3);
		Check(GetTree().CurrentScene is GameplayLevel && GameSession.SelectedLevel!.Id == $"crystal_palace_{tier}", "重试保留低高阶目的地");
		GetTree().CurrentScene.QueueFree(); GetTree().CurrentScene = null; await Wait(.1);
		GD.Print($"PASS water entrance {tier}");
	}

	private async Task Capture(string name)
	{
		string directory = ProjectSettings.GlobalizePath("res://.godot/dragon-checks");
		DirAccess.MakeDirRecursiveAbsolute(directory);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"{directory}/{name}.png") == Error.Ok, "保存巡检画面");
	}
}
