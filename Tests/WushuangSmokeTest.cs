using System;
using System.Linq;
using Godot;
using Zaomeng.Combat.Buffs;
using Zaomeng.Combat.Effects;
using Zaomeng.Combat.Wushuang;
using Zaomeng.Skills;
using Zaomeng.UI;

namespace Zaomeng;

/// <summary>不读写存档；通过真实结算、技能与 HUD 验证无双规则。</summary>
public partial class WushuangSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			VerifyResources();
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			player.MaxHealth = 10000; player.MaxMana = 1000; player.Attack = 0; player.Gravity = 0;
			player.SoundProfile = null;
			var silentAttack = (AttackStep)player.NormalCombo[0].Duplicate();
			silentAttack.Sound = null; player.NormalCombo = [silentAttack];
			player.InputEnabled = false; AddChild(player); player.Position = new(250, 350);
			player.SetPhysicsProcess(false);
			var monster = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			monster.MaxHealth = 100000; monster.Gravity = 0; monster.AiEnabled = false;
			monster.SoundProfile = null;
			AddChild(monster); monster.Position = new(310, 350); monster.SetPhysicsProcess(false);
			monster.DodgeRating = player.CriticalRating = 0; monster.Level = player.Level;
			using var fixedGain = new WushuangGain { Minimum = 7, Maximum = 7 };
			using var zeroGain = new WushuangGain();
			using var hit = new HitDefinition { AttackMultiplier = 0, FlatDamage = 10,
				DamageType = DamageType.True, CanCrit = false, Knockback = Vector2.Zero, Hitstun = 0 };
			using var skill = new SkillDefinition { WushuangGain = fixedGain };
			Check(!player.Wushuang.TryActivate(), "未满不能开启");
			CombatResolver.Resolve(player, monster, hit);
			Check(player.Wushuang.Value == 2, "未配置接触默认加2");
			CombatResolver.Resolve(player, monster, hit, sourceSkill: skill);
			Check(player.Wushuang.Value == 9, "获取由源技能属性决定");
			hit.WushuangGain = zeroGain; CombatResolver.Resolve(player, monster, hit, sourceSkill: skill);
			Check(player.Wushuang.Value == 9, "阶段零值覆写不会回退默认值");
			hit.WushuangGain = null;
			monster.DodgeRating = 1e10f; CombatResolver.Resolve(player, monster, hit, sourceSkill: skill);
			Check(player.Wushuang.Value == 16, "闪避仍获取无双"); monster.DodgeRating = 0;
			hit.FlatDamage = 0; CombatResolver.Resolve(player, monster, hit, sourceSkill: skill);
			Check(player.Wushuang.Value == 23, "零伤害接触仍获取"); hit.FlatDamage = 10;
			Check(!CombatResolver.Resolve(player, player, hit) && player.Wushuang.Value == 23, "无效目标不获取");
			player.Wushuang.Tick(0.49); Check(player.Wushuang.Value == 23, "未到半秒不衰减");
			player.Wushuang.Tick(0.01); Check(player.Wushuang.Value == 22, "旧整数减0.2实际减1");
			player.Wushuang.Gain(int.MaxValue); player.Wushuang.Tick(1);
			Check(player.Wushuang.Value == 100, "满值上限且保留");
			var hud = GD.Load<PackedScene>("res://Scenes/UI/Level/Role_information.tscn").Instantiate<Node2D>();
			AddChild(hud); var binding = new PlayerHud(); binding.Bind(hud, player); AddChild(binding);
			Check(hud.GetNode<AnimatedSprite2D>("roleLayer/role_menu/max_ws").Visible, "HUD满值提示");
			using var costumeBuff = new BuffDefinition { Id = "test_costume", DisplayName = "测试时装无双",
				DamageReduction = 0.2f, OutgoingDamageBonus = 0.2f };
			using var grant = new BuffGrantEffect { Buff = costumeBuff, OnlyDuringWushuang = true };
			using var costume = new PassiveSkillDefinition { Id = "test_costume_skill", NameKey = "测试时装",
				DescriptionKey = "无双时生效", Effect = grant };
			player.PassiveEffects.SetEquipment([costume]);
			Check(!player.Buffs.Active.Any(b => b.Definition == costumeBuff), "时装无双Buff平时不生效");
			Check(player.Wushuang.TryActivate() && !player.Wushuang.TryActivate(), "满值仅开启一次");
			Check(player.Buffs.SuperArmor && player.Buffs.MoveSpeedMultiplier == 1.5f &&
				player.Buffs.Active.Any(b => b.Definition == costumeBuff), "基础与时装Buff统一生效");
			var afterimages = player.GetNode<WushuangAfterimages>("WushuangAfterimages");
			afterimages._PhysicsProcess(0.1);
			Check(GetChildren().OfType<Node2D>().Any(n => n.Name.ToString().StartsWith("WushuangAfterimage")), "残影采样当前精灵");
			if (OS.GetCmdlineUserArgs().Contains("--capture-wushuang"))
			{
				player.Position += new Vector2(30, 0); afterimages._PhysicsProcess(0.1);
				player.Position += new Vector2(30, 0);
				binding._Process(0);
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				string directory = ProjectSettings.GlobalizePath("res://.godot/wushuang-checks");
				System.IO.Directory.CreateDirectory(directory);
				GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "active.png"));
				player.Position = new(250, 350);
			}
			player.PassiveEffects.SetEquipment([]);
			Check(!player.Buffs.Active.Any(b => b.Definition == costumeBuff) && player.Buffs.SuperArmor, "穿脱独立移除条件Buff");
			float health = monster.Health; CombatResolver.Resolve(player, monster, hit, sourceSkill: skill);
			Check(monster.Health == health - 15 && player.Wushuang.Value == 100, "旧结算后1.5倍伤害，开启中不获取");
			Check(player.TryAttack(), "无双中能攻击"); var state = player.State;
			health = player.Health; player.ReceiveHit(new(10, new(100, -100), 1));
			Check(player.Health == health - 10 && player.State == state && player.Motor.ExternalVelocityX == 0,
				"霸体仍掉血但不打断或击退");
			player.Wushuang.Tick(0.5); Check(player.Wushuang.Value == 96, "开启每半秒扣4");
			player.Wushuang.Gain(100); Check(player.Wushuang.Value == 96, "期间不能重新积攒");
			GetTree().Paused = true;
			await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
			Check(player.Wushuang.Value == 96, "暂停冻结无双时钟"); GetTree().Paused = false;
			binding._Process(0);
			Check(!hud.GetNode<AnimatedSprite2D>("roleLayer/role_menu/max_ws").Visible &&
				hud.GetNode<TextureProgressBar>("roleLayer/role_menu/ws_wk/ws_effect").Value == 96, "HUD开启后显示消耗并关闭满值提示");
			player.PassiveEffects.SetEquipment([costume]); player.Wushuang.Tick(12);
			Check(!player.Wushuang.IsActive && player.Wushuang.Value == 0 && !player.Buffs.SuperArmor &&
				!player.Buffs.Active.Any(b => b.Definition == costumeBuff), "耗尽清理基础与条件增益");
			player.ResetForSpawn(player.Position); player.Wushuang.Gain(10);
			using var poison = new BuffDefinition { Id = "test_poison", DisplayName = "测试中毒", DamagePerPulse = 5 };
			monster.Buffs.Apply(poison, "test", sourceActor: player, sourceSkill: skill); monster.Buffs.Tick(1);
			Check(player.Wushuang.Value == 10, "持续伤害不能误获技能无双"); monster.Buffs.Clear();
			player.Wushuang.Reset();
			var second = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			second.MaxHealth = 10000; second.AiEnabled = false; second.Gravity = 0;
			AddChild(second); second.Position = new(330, 350); second.SetPhysicsProcess(false);
			var projectile = GD.Load<PackedScene>("res://Content/Skills/Tangseng/IceDragonProjectile.tscn").Instantiate<SkillProjectile>();
			projectile.Configure(player, sourceSkill: skill);
			skill.WushuangGain = zeroGain;
			AddChild(projectile); projectile.Position = new(300, 315); projectile.SetPhysicsProcess(false);
			projectile._PhysicsProcess(0.001);
			Check(player.Wushuang.Value == 14, "弹体逐目标获取且保留发出时快照");
			projectile._PhysicsProcess(0.001);
			Check(player.Wushuang.Value == 14, "弹体同目标不重复获取");
			projectile.QueueFree(); second.Position = new(1500, 350); second.QueueFree(); skill.WushuangGain = fixedGain;
			player.Wushuang.Gain(100); Check(player.Wushuang.TryActivate(), "可以再次开启");
			player.ReceiveHit(new(player.MaxHealth * 2, Vector2.Zero, 0));
			Check(player.IsDead && !player.Wushuang.IsActive && player.Wushuang.Value == 0 && player.Buffs.Active.Count == 0,
				"霸体可死亡，死亡同步清零");
			player.ResetForSpawn(player.Position); Check(player.Wushuang.Value == 0, "重生清理无双");
			player.PassiveEffects.SetEquipment([]);
			player.Attack = 10; monster.Position = player.Position + new Vector2(60, 0);
			player.SetPhysicsProcess(true);
			using var firestorm = (SkillDefinition)GD.Load<SkillDefinition>("res://Content/Skills/Wukong/lyfb.tres").Duplicate();
			firestorm.Sound = null;
			firestorm.CooldownSeconds = 0;
			Check(player.TryUseSkill(firestorm), "真实烈焰风暴起招");
			await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
			Check(player.Wushuang.Value is >= 24 and <= 32, $"四段分别获取6–8，实际{player.Wushuang.Value}");
			player.SetPhysicsProcess(false); player.ResetForSpawn(player.Position);
			using var lethal = new HitDefinition { FlatDamage = 1000000, DamageType = DamageType.True, CanCrit = false };
			CombatResolver.Resolve(player, monster, lethal, sourceSkill: skill);
			Check(monster.IsDead && player.Wushuang.Value == 7, "致死接触也获取");
			CombatResolver.Resolve(player, monster, lethal, sourceSkill: skill);
			Check(player.Wushuang.Value == 7, "尸体不重复获取");
			foreach (Node voice in Zaomeng.Audio.AudioManager.Instance!.GetChildren())
				if (voice is AudioStreamPlayer audio) { audio.Stop(); audio.Stream = null; }
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			// 原生引擎关闭前回收临时数组，避免渲染测试退出后终结器访问已卸载的绑定。
			GC.Collect(); GC.WaitForPendingFinalizers();
			GD.Print("WUSHUANG SMOKE TEST PASSED"); GetTree().Quit();
		}
		catch (Exception error) { GetTree().Paused = false; GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void VerifyResources()
	{
		foreach (var (role, id, min, max) in new[] {
			("Wukong", "slz", 6, 8), ("Wukong", "hytj", 2, 4), ("Wukong", "lys", 15, 20),
			("Wukong", "lyfb", 6, 8), ("Wukong", "hmz", 6, 8), ("Wukong", "zz", 15, 18),
			("Wukong", "qsez", 8, 10), ("Wukong", "jdy", 8, 10), ("Wukong", "hyjj", 8, 10),
			("Tangseng", "blb", 15, 18), ("Tangseng", "xbz", 10, 12), ("Tangseng", "jhsj", 10, 15) })
		{
			var gain = GD.Load<SkillDefinition>($"res://Content/Skills/{role}/{id}.tres").WushuangGain;
			Check(gain?.Minimum == min && gain.Maximum == max, $"{id}旧版区间");
		}
		var slash = GD.Load<PackedScene>("res://Content/Skills/Behaviors/FireSlash.tscn").Instantiate<FireSlashBehavior>();
		Check(slash.LandingHit.WushuangGain is { Minimum: 10, Maximum: 15 }, "火魔斩落地覆写"); slash.Free();
		for (int stage = 1; stage <= 4; stage++)
			Check(GD.Load<AttackStep>($"res://Content/Attacks/Wukong/hit{stage}.tres").Hit.WushuangGain is { Minimum: 3, Maximum: 5 }, "悟空普攻旧值");
		Check(GD.Load<AttackStep>("res://Content/Attacks/Tangseng/hit1.tres").Hit.WushuangGain is { Minimum: 2, Maximum: 4 }, "唐僧普攻旧值");
		Check(GD.Load<Zaomeng.Equipment.MagicWeaponAbility>("res://Content/MagicWeapons/zjhl.tres").Action.WushuangGain is { Minimum: 15, Maximum: 20 }, "紫金葫芦旧值");
		Check(InputMap.ActionGetEvents("jump").OfType<InputEventKey>().All(k => k.PhysicalKeycode != Key.Space) &&
			InputMap.ActionGetEvents("wushuang").OfType<InputEventKey>().Any(k => k.PhysicalKeycode == Key.Space), "空格无双与K跳跃不冲突");
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print("PASS: " + message);
	}
}
