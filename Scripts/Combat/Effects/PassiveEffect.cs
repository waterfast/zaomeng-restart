using Godot;

namespace Zaomeng.Combat.Effects;

/// <summary>资源只存配置；公共运行集合负责调用，效果不自行订阅角色事件。</summary>
[GlobalClass]
[Tool]
public abstract partial class PassiveEffect : Resource
{
	public abstract void Validate();
	public virtual AttackParameters ModifyAttackParameters(CharacterActor owner, SkillDefinition? skill, AttackParameters parameters) => parameters;
	public virtual float ModifyOutgoingDamage(CharacterActor owner, CharacterActor target, SkillDefinition? skill, float damage) => damage;
	public virtual void OnHitDealt(CharacterActor owner, CharacterActor target, HitResult hit) { }
	public virtual string FormatDescription(string template) => template;
}
