using Godot;

namespace Zaomeng.Equipment.Skills;

/// <summary>装备触发效果只保存策划参数，角色运行状态不得写入共享资源。</summary>
[GlobalClass]
public abstract partial class EquipmentSkillEffect : Resource
{
	public abstract void Validate();
	public virtual float ModifyOutgoingDamage(Player owner, CharacterActor target, SkillDefinition? sourceSkill, float damage) => damage;
	public virtual void OnHitDealt(Player owner, CharacterActor target, HitResult hit) { }
	public virtual string FormatDescription(string template) => template;
}
