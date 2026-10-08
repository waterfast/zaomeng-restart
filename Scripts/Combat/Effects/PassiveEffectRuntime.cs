using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaomeng.Combat.Effects;

/// <summary>每个角色拥有自己的来源集合；同步调用所有效果，重建不产生事件订阅。</summary>
public sealed class PassiveEffectRuntime
{
	private readonly CharacterActor _owner;
	private PassiveSkillDefinition[] _innate = [];
	private PassiveSkillDefinition[] _equipment = [];
	private PassiveSkillDefinition[] _active = [];
	public IReadOnlyList<PassiveSkillDefinition> Active => Array.AsReadOnly(_active);
	public PassiveEffectRuntime(CharacterActor owner) => _owner = owner;
	public void SetInnate(IEnumerable<PassiveSkillDefinition> skills) => Replace(skills.ToArray(), _equipment, true);
	public void SetEquipment(IEnumerable<PassiveSkillDefinition> skills) => Replace(_innate, skills.ToArray(), false);
	private void Replace(PassiveSkillDefinition[] innate, PassiveSkillDefinition[] equipment, bool replacingInnate)
	{
		var byId = new Dictionary<string, PassiveSkillDefinition>(StringComparer.Ordinal);
		foreach (var skill in innate.Concat(equipment))
		{
			if (skill is null) throw new InvalidOperationException("被动来源包含空技能。");
			skill.Validate();
			if (byId.TryGetValue(skill.Id, out var other) && other != skill)
				throw new InvalidOperationException($"被动技能 ID {skill.Id} 对应不同定义。");
			byId[skill.Id] = skill;
		}
		var active = byId.Values.OrderBy(s => s.Priority).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
		// 所有来源验证成功后提交，失败保留原集合。
		if (replacingInnate) _innate = innate; else _equipment = equipment;
		_active = active;
		RefreshBuffSources();
	}
	public void RefreshBuffSources() => _owner.Buffs.SetPermanentSource("passive",
		_active.Select(s => s.Effect).OfType<BuffGrantEffect>()
			.Where(e => !e.OnlyDuringWushuang || _owner is Player { Wushuang.IsActive: true }).Select(e => e.Buff));
	public AttackParameters PrepareAttack(SkillDefinition? skill)
	{
		var parameters = AttackParameters.Default;
		foreach (var passive in _active)
		{
			parameters = passive.Effect.ModifyAttackParameters(_owner, skill, parameters) ??
				throw new InvalidOperationException($"被动 {passive.Id} 返回空攻击参数。");
			parameters.Validate();
		}
		return parameters;
	}
	public float ModifyOutgoingDamage(CharacterActor target, SkillDefinition? skill, float damage)
	{
		foreach (var passive in _active)
		{
			damage = passive.Effect.ModifyOutgoingDamage(_owner, target, skill, damage);
			if (!float.IsFinite(damage) || damage < 0) throw new InvalidOperationException($"被动 {passive.Id} 返回无效伤害。");
		}
		return damage;
	}
	public void OnHitDealt(CharacterActor target, HitResult hit)
	{
		foreach (var passive in _active) passive.Effect.OnHitDealt(_owner, target, hit);
	}
}
