using System;
using Godot;
using Zaomeng.Level;

namespace Zaomeng.Monsters;

[GlobalClass]
public partial class MonsterDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public PackedScene ActorScene { get; set; } = null!;
	[Export] public bool IsBoss { get; set; }
	[Export] public float Health { get; set; } = 60;
	[Export] public float Attack { get; set; } = 10;
	[Export] public int Level { get; set; } = 5;
	[Export] public float PhysicalDefense { get; set; }
	[Export] public float MagicDefense { get; set; }
	[Export] public float CriticalRating { get; set; }
	[Export] public float CriticalResistance { get; set; }
	[Export] public float DodgeRating { get; set; }
	[Export] public float Accuracy { get; set; }
	[Export] public float Toughness { get; set; }
	[Export] public float ArmorPenetration { get; set; }
	[Export] public float MagicPenetration { get; set; }
	[Export] public float LifeSteal { get; set; }
	[Export] public float Luck { get; set; }
	[Export] public float MoveSpeed { get; set; } = 85;
	[Export] public float DetectionRange { get; set; } = 550;
	[Export] public int Experience { get; set; } = 1;
	[Export] public int Souls { get; set; } = 1;
	[Export] public Godot.Collections.Array<MonsterSkillChoice> Skills { get; set; } = new();
	[Export] public Godot.Collections.Array<Zaomeng.Combat.Effects.PassiveSkillDefinition> PassiveSkills { get; set; } = new();
	[Export] public MonsterDropTable? Drops { get; set; }
	[Export] public MonsterDropTable? RecoveryDrops { get; set; }
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Id) || ActorScene is null || !ActorScene.CanInstantiate()
			|| !float.IsFinite(Health) || Health <= 0 || !float.IsFinite(Attack) || Attack < 0
			|| Level < 1 || !float.IsFinite(MoveSpeed) || MoveSpeed < 0
			|| !float.IsFinite(DetectionRange) || DetectionRange <= 0 || Experience < 0 || Souls < 0)
			throw new InvalidOperationException($"怪物模板 {Id} 配置无效。");
		foreach (float stat in new[] { PhysicalDefense, MagicDefense, CriticalRating, CriticalResistance,
			DodgeRating, Accuracy, Toughness, ArmorPenetration, MagicPenetration, LifeSteal, Luck })
			if (!float.IsFinite(stat) || stat < 0) throw new InvalidOperationException($"怪物 {Id} 属性无效。");
		if (Skills.Count == 0) throw new InvalidOperationException($"怪物 {Id} 没有配置技能。");
		var actor = ActorScene.Instantiate();
		try
		{
			if (actor is not Monster || actor.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is not { } animator)
				throw new InvalidOperationException($"怪物 {Id} 表现场景缺少战斗组件。");
			new Zaomeng.Combat.Effects.PassiveEffectRuntime((Monster)actor).SetInnate(PassiveSkills);
			foreach (var choice in Skills)
			{
				if (choice is null) throw new InvalidOperationException($"怪物 {Id} 存在空技能。");
				choice.Validate();
				if (!animator.HasAnimation(choice.Skill.Animation))
					throw new InvalidOperationException($"怪物 {Id} 缺少技能动画 {choice.Skill.Animation}。");
				var skill = choice.Skill;
				double length = animator.GetAnimation(skill.Animation).Length;
				if (skill.FramesPerSecond <= 0 || !double.IsFinite(length) || length <= 0)
					throw new InvalidOperationException($"怪物 {Id} 技能帧率或动画时长无效。");
				int previousEnd = 0;
				foreach (var window in skill.Hits)
				{
					if (window?.Hit is null || window.StartFrame < previousEnd || window.EndFrame <= window.StartFrame
						|| window.StartFrame / (double)skill.FramesPerSecond >= length)
						throw new InvalidOperationException($"怪物 {Id} 技能命中窗口无效。");
					previousEnd = window.EndFrame;
				}
				foreach (var motion in new[] { skill.Motion, skill.AirMotion })
					if (motion is not null && !motion.IsValidFor(length, skill.FramesPerSecond))
						throw new InvalidOperationException($"怪物 {Id} 技能位移无效。");
				if (choice.Skill.BehaviorScene is not { } scene) continue;
				var behavior = scene.Instantiate();
				try
				{
					if (behavior is not Zaomeng.Skills.SkillBehavior configured)
						throw new InvalidOperationException($"怪物 {Id} 的技能行为类型无效。");
					configured.ValidateConfiguration();
				}
				finally { behavior.Free(); }
			}
		}
		finally { actor.Free(); }
		Drops?.Validate();
		RecoveryDrops?.Validate();
	}
	public void Apply(Monster actor, LevelDifficultyDefinition? difficulty)
	{
		actor.Definition = this;
		actor.PassiveSkills = PassiveSkills;
		actor.PassiveEffects.SetInnate(PassiveSkills);
		actor.DisplayName = DisplayName;
		actor.IsBoss = IsBoss;
		actor.MaxHealth = Health * (difficulty?.HealthMultiplier(IsBoss) ?? 1);
		actor.Attack = Attack * (difficulty?.AttackMultiplier(IsBoss) ?? 1);
		actor.Level = difficulty?.ResolveLevel(IsBoss, Level) ?? Level;
		actor.PhysicalDefense = PhysicalDefense * (difficulty?.DefenseMultiplier(IsBoss) ?? 1);
		actor.MagicDefense = MagicDefense * (difficulty?.DefenseMultiplier(IsBoss) ?? 1);
		actor.CriticalRating = CriticalRating;
		actor.CriticalResistance = CriticalResistance;
		actor.DodgeRating = DodgeRating;
		actor.Accuracy = Accuracy;
		actor.Toughness = Toughness;
		actor.ArmorPenetration = ArmorPenetration;
		actor.MagicPenetration = MagicPenetration;
		actor.LifeSteal = LifeSteal;
		actor.Luck = Luck;
		actor.MoveSpeed = MoveSpeed;
		actor.DetectionRange = DetectionRange;
		actor.ExperienceReward = Experience;
		actor.SoulValuePerOrb = Souls;
	}
}
