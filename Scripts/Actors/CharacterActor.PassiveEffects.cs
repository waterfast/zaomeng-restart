using System.Collections.Generic;
using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng;

public partial class CharacterActor
{
	[Export] public Godot.Collections.Array<PassiveSkillDefinition> PassiveSkills { get; set; } = new();
	// 指向未被动作轨道驱动的缩放节点，不能直接乘身体或动画自身的 Scale。
	[Export] public Godot.Collections.Array<NodePath> AttackVisualScaleRoots { get; set; } = new();
	public PassiveEffectRuntime PassiveEffects { get; }
	public AttackParameters CurrentAttackParameters { get; private set; } = AttackParameters.Default;
	private readonly List<(Node2D Root, Vector2 BaseScale)> _attackVisualRoots = new();
	public Zaomeng.Combat.Buffs.BuffRuntime Buffs { get; }
	public CharacterActor()
	{
		PassiveEffects = new(this);
		Buffs = new(this);
	}
	private void InitializePassiveEffects()
	{
		PassiveEffects.SetInnate(PassiveSkills);
		foreach (var path in AttackVisualScaleRoots)
		{
			var root = GetNode<Node2D>(path);
			_attackVisualRoots.Add((root, root.Scale));
		}
	}
	private void PrepareAttackParameters(SkillDefinition? skill)
	{
		CurrentAttackParameters = PassiveEffects.PrepareAttack(skill);
		AttackBox.Scale = Vector2.One * CurrentAttackParameters.Range;
		foreach (var (root, baseScale) in _attackVisualRoots) root.Scale = baseScale * CurrentAttackParameters.VisualScale;
	}
	private void ResetAttackParameters()
	{
		CurrentAttackParameters = AttackParameters.Default;
		if (AttackBox is not null) AttackBox.Scale = Vector2.One;
		if (Animator is not null) Animator.SpeedScale = 1;
		foreach (var (root, baseScale) in _attackVisualRoots) root.Scale = baseScale;
	}
}
