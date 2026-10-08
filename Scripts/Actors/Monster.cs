using Godot;
using System.Collections.Generic;
using Zaomeng.Monsters;

namespace Zaomeng;

public partial class Monster : CharacterActor
{
	[Export] public bool IsBoss { get; set; }
	[Export] public string DisplayName { get; set; } = "小怪";
	[Export] public int ExperienceReward { get; set; } = 1;
	[Export] public int SoulValuePerOrb { get; set; } = 1;
	[Export] public NodePath TargetPath { get; set; } = new("../Player");
	[Export] public float DetectionRange { get; set; } = 550;
	[Export] public bool AiEnabled { get; set; } = true;
	private CharacterActor? _target;
	public MonsterDefinition? Definition { get; set; }
	public bool GrantsRewards { get; set; } = true;
	public string CombatMode { get; set; } = "";
	public System.Action<MonsterDefinition, int>? Summon { get; set; }
	private readonly record struct CooldownState(float Remaining, float Duration, bool Pending);
	private readonly Dictionary<SkillDefinition, CooldownState> _skillCooldowns = new();
	private int _skillLevel = 1;
	protected override int GetSkillLevel(SkillDefinition skill) => _skillLevel;
	public override void _PhysicsProcess(double delta)
	{
		foreach (var skill in new List<SkillDefinition>(_skillCooldowns.Keys))
		{
			var state = _skillCooldowns[skill];
			if (state.Pending) continue;
			float remaining = state.Remaining - (float)delta;
			if (remaining <= 0) _skillCooldowns.Remove(skill);
			else _skillCooldowns[skill] = state with { Remaining = remaining };
		}
		base._PhysicsProcess(delta);
	}

	private bool IsCoolingDown(SkillDefinition skill) => _skillCooldowns.TryGetValue(skill, out var state)
		&& (state.Pending || state.Remaining > 0);

	public override bool TryUseSkill(SkillDefinition? skill)
	{
		if (skill is null || IsCoolingDown(skill)) return false;
		// 行为可能在起招时通知冷却开始，先建立快照；动作校验失败则撤销。
		_skillCooldowns[skill] = new(skill.StartCooldownOnImpact ? 0 : skill.CooldownSeconds,
			skill.CooldownSeconds, skill.StartCooldownOnImpact);
		if (base.TryUseSkill(skill)) return true;
		_skillCooldowns.Remove(skill);
		return false;
	}

	public override void StartPendingSkillCooldown(SkillDefinition skill)
	{
		if (_skillCooldowns.TryGetValue(skill, out var state) && state.Pending)
			_skillCooldowns[skill] = state with { Remaining = state.Duration, Pending = false };
	}

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("monsters");
		_target = GetNodeOrNull<CharacterActor>(TargetPath);
	}

	public override void ResetForSpawn(Vector2 position)
	{
		base.ResetForSpawn(position);
		// 死亡动画淡出了身体，对象池再次生成时必须恢复，否则留下透明的小怪。
		GetNode<AnimatedSprite2D>("Facing/Visual/Body").SelfModulate = Colors.White;
		_skillCooldowns.Clear();
		GrantsRewards = true;
		CombatMode = "";
		foreach (var child in GetChildren())
			if (child is Zaomeng.Monsters.MonsterMechanic mechanic) mechanic.ResetForSpawn();
	}

	protected override void ReadIntent(float delta)
	{
		if (!AiEnabled || Definition is not { } template || !IsInstanceValid(_target) || _target!.IsDead) return;
		Vector2 offset = _target.GlobalPosition - GlobalPosition;
		if (offset.Length() > DetectionRange) return;
		Face(Mathf.Sign(offset.X));
		float distance = Mathf.Abs(offset.X), healthRatio = Health / MaxHealth;
		float pursuitRange = float.MaxValue;
		foreach (var choice in template.Skills)
		{
			if (choice.RequiredMode.Length > 0 && choice.RequiredMode != CombatMode) continue;
			float range = PassiveEffects.PrepareAttack(choice.Skill).Range;
			// 远程技能冷却时继续接近普通攻击距离，不能停在所有技能的最远范围。
			pursuitRange = Mathf.Min(pursuitRange, choice.MaximumRange * range);
			if (distance < choice.MinimumRange || distance > choice.MaximumRange * range ||
				Mathf.Abs(offset.Y) > choice.MaximumHeightDifference * range || healthRatio < choice.MinimumHealthRatio ||
				healthRatio > choice.MaximumHealthRatio || IsCoolingDown(choice.Skill)) continue;
			_skillLevel = choice.Level;
			if (!TryUseSkill(choice.Skill)) continue;
			return;
		}
		if (distance > pursuitRange) MoveDirection = Mathf.Sign(offset.X);
		return;
	}
}
