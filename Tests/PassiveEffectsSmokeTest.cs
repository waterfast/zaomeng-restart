using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Combat.Effects;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Skills;

namespace Zaomeng;

/// <summary>真实演员、动画、碰撞和弹体验证，不读写存档。</summary>
public partial class PassiveEffectsSmokeTest : Node2D
{
	private Monster? _boss;
	public override async void _Ready()
	{
		try
		{
			TranslationServer.SetLocale("zh_CN");
			var definition = GD.Load<MonsterDefinition>("res://Content/Monsters/Templates/macaque_king.tres");
			definition.Validate();
			var rage = definition.PassiveSkills.Single();
			Check(rage.DisplayName == "通风大圣" && rage.Description.Contains("20%") && rage.Description.Contains("100%"), "resource name and description");
			var pool = new MonsterPool(this);
			var boss = pool.Spawn(definition, new(350, 400));
			_boss = boss;
			boss.AiEnabled = false;
			boss.SetPhysicsProcess(false);
			var target = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			target.MaxHealth = 100000;
			AddChild(target);
			target.InputEnabled = false;
			target.SetPhysicsProcess(false);
			target.Level = boss.Level;
			boss.CriticalRating = target.CriticalRating = 0;
			target.DodgeRating = target.PhysicalDefense = target.MagicDefense = 0;
			var wind = definition.Skills.Single(c => c.Skill.Id == "macaque_wind").Skill;
			var strike = definition.Skills.Single(c => c.Skill.Id == "macaque_strike").Skill;
			var hit = new HitDefinition { DamageType = DamageType.True, CanCrit = false, Knockback = Vector2.Zero };
			LowerTo(boss, 0.2f);
			Check(Near(boss.PassiveEffects.PrepareAttack(wind).AttackSpeed, 1), "exactly 20 percent is not active");
			float before = target.Health;
			CombatResolver.Resolve(boss, target, hit, sourceSkill: wind);
			float normalDamage = before - target.Health;
			LowerTo(boss, 0.19f);
			Check(boss.TryUseSkill(wind), "rage cast starts");
			Check(Near(boss.Animator.SpeedScale, 1.5f) && Near(boss.AttackBox.Scale.X, 1.5f), "animation and collision use rage parameters");
			Check(Near(boss.GetNode<Node2D>("Facing/Visual/WindCharge/Modifier").Scale.X, 1.5f), "hand ball uses isolated visual scale");
			before = target.Health;
			CombatResolver.Resolve(boss, target, hit, sourceSkill: wind);
			Check(Near(before - target.Health, normalDamage * 2), "monster damage doubles");
			await Wait(0.2);
			Check(boss.Animator.CurrentAnimationPosition > 0.25, "real animation clock advances faster");
			boss.ReceiveHit(new HitResult(1, Vector2.Zero, 0, DamageType.True));
			Check(Near(boss.Animator.SpeedScale, 1) && boss.AttackBox.Scale == Vector2.One, "interrupt restores speed and range");
			Check(boss.GetNode<Node2D>("Facing/Visual/WindCharge/Modifier").Scale == Vector2.One, "interrupt restores visual scale");
			boss.Heal(boss.MaxHealth);
			Check(Near(boss.PassiveEffects.PrepareAttack(wind).AttackSpeed, 1), "healing removes health condition");

			// 在原判定外、强化判定内放木桩，不能只比较 Scale 属性。
			LowerTo(boss, 1);
			target.ResetForSpawn(new(555, 400));
			boss.Face(1);
			before = target.Health;
			Check(boss.TryUseSkill(strike), "normal melee starts");
			await Wait(0.5);
			Check(Near(target.Health, before), "normal melee cannot reach distant target");
			LowerTo(boss, 0.19f);
			boss.Face(1);
			Check(boss.TryUseSkill(strike), "rage melee starts");
			await Wait(0.4);
			Check(target.Health < before, "expanded melee really hits distant target");

			// 装备和固有被动走同一个集合；当前装备增伤仍在每次命中时计算。
			var storm = GD.Load<SkillDefinition>("res://Content/Skills/Wukong/lyfb.tres");
			var fury = GD.Load<PassiveSkillDefinition>("res://Content/GameData/EquipmentSkills/earth_fury.tres");
			target.PassiveEffects.SetInnate([rage]);
			target.PassiveEffects.SetEquipment([rage, fury]);
			Check(target.PassiveEffects.Active.Count == 2, "same passive from multiple sources deduplicates");
			target.ResetForSpawn(new(800, 400));
			target.ReceiveHit(new HitResult(81000, Vector2.Zero, 0, DamageType.True));
			target.Attack = 100;
			LowerTo(boss, 1);
			boss.Level = target.Level;
			boss.PhysicalDefense = boss.MagicDefense = boss.DodgeRating = 0;
			before = boss.Health;
			CombatResolver.Resolve(target, boss, hit, sourceSkill: storm);
			Check(Near(before - boss.Health, 300), "player rage and equipment damage multiply together");
			boss.Heal(boss.MaxHealth);
			before = boss.Health;
			CombatResolver.Resolve(target, boss, hit, sourceSkill: wind);
			Check(Near(before - boss.Health, 200), "equipment target filter preserves other skills");
			target.PassiveEffects.SetEquipment([]);
			Check(target.PassiveEffects.Active.Count == 1, "unequip preserves innate passive");
			var impostor = Definition(rage.Id, new SkillParameterEffect());
			bool rejected = false;
			try { target.PassiveEffects.SetEquipment([impostor]); } catch (InvalidOperationException) { rejected = true; }
			Check(rejected && target.PassiveEffects.Active.Single() == rage, "invalid rebuild is atomic");
			CheckInvalidConfiguration();

			// 参数实际被各消费端使用，已经生成的弹体保留快照。
			LowerTo(boss, 1);
			var modifiers = new AttackParameterModifiers { RangeMultiplier = 2, VisualScaleMultiplier = 2,
				ProjectileSpeedMultiplier = 2, ProjectileLifetimeMultiplier = 0.2f, EffectSpeedMultiplier = 2,
				EffectLifetimeMultiplier = 0.2f, MotionSpeedMultiplier = 2 };
			boss.PassiveEffects.SetInnate([Definition("test_parameters", new SkillParameterEffect { Modifiers = modifiers })]);
			var parameters = boss.PassiveEffects.PrepareAttack(wind);
			var projectile = GD.Load<PackedScene>("res://Scenes/Effects/MacaqueWind.tscn").Instantiate<SkillProjectile>();
			boss.Face(1);
			projectile.Configure(boss, 1, wind, parameters);
			AddChild(projectile);
			projectile.GlobalPosition = new(1000, 0);
			projectile.SetPhysicsProcess(false);
			projectile._PhysicsProcess(0.05);
			Check(Near(projectile.GlobalPosition.X, 1100), "projectile speed is consumed");
			var sprite = projectile.GetChild<AnimatedSprite2D>(0);
			Check(Near(Mathf.Abs(sprite.Scale.X), 2) && Near(sprite.SpeedScale, 2), "projectile visual and playback parameters are consumed");
			boss.PassiveEffects.SetInnate([]);
			Check(Near(projectile.Parameters.Range, 2), "existing projectile retains snapshot after source changes");
			projectile._PhysicsProcess(0.2);
			Check(projectile.IsQueuedForDeletion(), "projectile lifetime is consumed");

			boss.PassiveEffects.SetInnate([Definition("test_parameters", new SkillParameterEffect { Modifiers = modifiers })]);
			var alternate = GD.Load<PackedScene>("res://Scenes/Effects/MacaqueWind.tscn").Instantiate<SkillProjectile>();
			alternate.Name = "AlternateProjectile";
			var alternateScene = new PackedScene();
			Check(alternateScene.Pack(alternate) == Error.Ok, "alternate projectile scene packs");
			alternate.Free();
			modifiers.ProjectileSceneOverride = alternateScene;
			LowerTo(boss, 1);
			boss.Face(1);
			Check(boss.TryUseSkill(wind), "projectile override cast starts");
			await Wait(0.76);
			Check(GetChildren().OfType<SkillProjectile>().Any(p => p.Name.ToString().StartsWith("AlternateProjectile", StringComparison.Ordinal)),
				"release uses replacement projectile scene");
			LowerTo(boss, 1);
			var dash = new SkillDefinition { Animation = "tfhx", FramesPerSecond = 60,
				Motion = new ActionMotion { Mode = ActionMotionMode.FacingDash, Speed = 100, StartFrame = 0 } };
			boss.Face(1);
			Check(boss.TryUseSkill(dash), "modified dash starts");
			boss._PhysicsProcess(0);
			Check(Near(boss.Velocity.X, 200), "motion speed parameter reaches character motor");
			LowerTo(boss, 1);
			target.ResetForSpawn(new(900, 400));
			var areaFrames = GD.Load<SpriteFrames>("res://Content/Monsters/TamarinEruptionFrames.tres");
			var area = new AreaAttackEffect();
			area.Configure(boss, wind, 1, 60, [new HitEvent { StartFrame = 0, EndFrame = 2, Hit = hit }],
				areaFrames, "Monster5Bullet", Vector2.One, Vector2.Zero, parameters);
			AddChild(area);
			area.GlobalPosition = new(800, 400);
			area.SetPhysicsProcess(false);
			before = target.Health;
			area._PhysicsProcess(0);
			Check(target.Health < before, "expanded area really reaches target outside base radius");
			Check(Near(area.GetChild<AnimatedSprite2D>(0).Scale.X, 2), "area visual size is consumed");
			area._PhysicsProcess(0.2);
			Check(area.IsQueuedForDeletion(), "area playback and lifetime are consumed");

			var replacement = VisualScene("Replacement");
			var lower = Definition("a_override", new SkillParameterEffect { Modifiers = new() { EffectSceneOverride = VisualScene("Lower") } });
			var upper = Definition("b_override", new SkillParameterEffect { Modifiers = new() { EffectSceneOverride = replacement } });
			upper.Priority = 10;
			boss.PassiveEffects.SetInnate([upper, lower]);
			Check(boss.PassiveEffects.PrepareAttack(wind).EffectScene == replacement, "replacement follows explicit priority");
			ActorEffectSpawner.SpawnAttached(replacement, this, Vector2.Zero, 0.1f, parameters);
			Check(Near(GetNode<Node2D>("Replacement").Scale.X, 2), "generic effect scale is consumed");
			await Wait(0.08);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(!HasNode("Replacement"), "generic effect lifetime is consumed");
			var untouched = GD.Load<PackedScene>("res://Scenes/Effects/MacaqueWind.tscn").Instantiate<SkillProjectile>();
			Check(Near(((SkillParameterEffect)rage.Effect).Modifiers.AttackSpeedMultiplier, 1.5f) &&
				Near(untouched.Speed, 1000), "shared parameters remain unchanged");
			untouched.Free();
			pool.Release(definition, boss);
			boss = pool.Spawn(definition, new(350, 400));
			boss.AiEnabled = false;
			Check(boss.PassiveEffects.Active.Single() == rage && boss.Animator.SpeedScale == 1 && boss.AttackBox.Scale == Vector2.One,
				"pool reuse restores template passive and attack state");
			GD.Print("PASSIVE EFFECTS SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void LowerTo(Monster boss, float ratio)
	{
		boss.ResetForSpawn(new(350, 400));
		if (ratio < 1) boss.ReceiveHit(new HitResult(boss.MaxHealth * (1 - ratio), Vector2.Zero, 0, DamageType.True));
		boss._PhysicsProcess(0);
	}
	private static PassiveSkillDefinition Definition(string id, PassiveEffect effect) => new()
	{
		Id = id, NameKey = "SKILL_WIND_SAGE_NAME", DescriptionKey = "SKILL_WIND_SAGE_DESCRIPTION", Effect = effect
	};
	private static PackedScene VisualScene(string name)
	{
		var root = new Node2D { Name = name };
		var scene = new PackedScene();
		Check(scene.Pack(root) == Error.Ok, "test scene packs");
		root.Free();
		return scene;
	}
	private static void CheckInvalidConfiguration()
	{
		foreach (var modifiers in new[] { new AttackParameterModifiers { RangeMultiplier = 0 }, new AttackParameterModifiers { AttackSpeedMultiplier = float.NaN } })
		{
			bool rejected = false;
			try { modifiers.Validate(); } catch (InvalidOperationException) { rejected = true; }
			Check(rejected, "invalid parameter rejected");
		}
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	public override void _PhysicsProcess(double delta)
	{
		// 固定测试演员的位置，同时保留每帧技能窗口更新；动画与命中框独立运行。
		if (_boss is not null && !_boss.IsPhysicsProcessing()) _boss._PhysicsProcess(0);
	}
	private static bool Near(float value, float expected) => Mathf.Abs(value - expected) < 0.01f;
	private static void Check(bool valid, string message)
	{
		if (!valid) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
