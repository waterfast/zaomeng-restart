using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Zaomeng.Combat.Buffs;

public readonly record struct BuffView(BuffDefinition Definition, float Remaining, int Level);

/// <summary>状态归角色；资源仅保存配置。来源独立计时，相同 Buff 不重复加成。</summary>
public sealed class BuffRuntime(CharacterActor owner)
{
	private sealed class State(BuffDefinition definition, string source, int level, bool permanent)
	{
		public BuffDefinition Definition = definition;
		public string Source = source;
		public int Level = level;
		public float Remaining = permanent ? float.PositiveInfinity : definition.Duration;
		public float UntilPulse = definition.PulseImmediately ? 0 : definition.PulseInterval;
		public SkillDefinition? SourceSkill;
		public float RangeMultiplier = 1;
		public CharacterActor? SourceActor;
		public int SourceRevision;
	}
	private readonly List<State> _states = new();
	private double _experienceRemainder, _soulRemainder;
	public IReadOnlyList<BuffView> Active => Effective().Select(s => new BuffView(s.Definition, s.Remaining, s.Level)).ToArray();
	private IEnumerable<State> Effective() => _states.GroupBy(s => s.Definition.Id, StringComparer.Ordinal)
		.Select(group => group.OrderByDescending(s => s.Level).ThenByDescending(s => s.Remaining).First());
	public float ExperienceMultiplier => 1 + Effective().Sum(s => s.Definition.ExperienceBonus);
	public float SoulMultiplier => 1 + Effective().Sum(s => s.Definition.SoulBonus);
	public float HasteBonus => Effective().Sum(s => s.Definition.HasteBonus);
	public float OutgoingMultiplier => 1 + Effective().Sum(s => s.Definition.OutgoingDamageBonus);
	public float FinalDamageMultiplier => Effective().Aggregate(1f, (value, s) => value * s.Definition.FinalDamageMultiplier);
	public bool SuperArmor => Effective().Any(s => s.Definition.SuperArmor);
	public float IncomingMultiplier => Mathf.Max(0, 1 - Effective().Sum(s => s.Definition.DamageReduction));
	public bool PreventsActions => Effective().Any(s => s.Definition.PreventsActions);
	public float MoveSpeedMultiplier => Effective().Aggregate(1f, (value, s) => value * s.Definition.MoveSpeedMultiplier);
	public StringName ForcedAnimation => Effective().Select(s => s.Definition.ForcedAnimation).FirstOrDefault(name => !name.IsEmpty) ?? new StringName("");
	public void Apply(BuffDefinition definition, string source, int level = 1, bool permanent = false,
		SkillDefinition? sourceSkill = null, float rangeMultiplier = 1, CharacterActor? sourceActor = null)
	{
		definition.Validate();
		if (string.IsNullOrWhiteSpace(source) || level < 1 || !float.IsFinite(rangeMultiplier) || rangeMultiplier <= 0 || (!permanent && definition.Duration <= 0))
			throw new InvalidOperationException("Buff 来源、等级或持续时间无效。");
		if (_states.Any(s => s.Definition.Id == definition.Id && s.Definition != definition))
			throw new InvalidOperationException($"Buff ID {definition.Id} 对应不同资源。");
		var existing = _states.Find(s => s.Source == source && s.Definition.Id == definition.Id);
		if (existing is null) { existing = new(definition, source, level, permanent); _states.Add(existing); }
		else { existing.Remaining = permanent ? float.PositiveInfinity : definition.Duration; existing.Level = level; }
		existing.SourceSkill = sourceSkill;
		existing.RangeMultiplier = rangeMultiplier;
		existing.SourceActor = sourceActor;
		existing.SourceRevision = sourceActor?.SpawnRevision ?? 0;
	}
	public void SetPermanentSource(string source, IEnumerable<BuffDefinition> definitions)
	{
		var next = definitions.Distinct().ToArray();
		foreach (var definition in next) definition.Validate();
		if (next.GroupBy(d => d.Id).Any(g => g.Count() > 1) || next.Any(d => _states.Any(s => s.Source != source && s.Definition.Id == d.Id && s.Definition != d)))
			throw new InvalidOperationException("Buff 来源存在冲突 ID。");
		RemoveSource(source);
		foreach (var definition in next) Apply(definition, source, permanent: true);
	}
	public void RemoveSource(string source) => _states.RemoveAll(s => s.Source == source);
	public void Clear()
	{
		_states.Clear();
		_experienceRemainder = _soulRemainder = 0;
	}
	public long ScaleExperience(long amount) => ScaleReward(amount, ExperienceMultiplier, ref _experienceRemainder);
	public int ScaleSouls(int amount) => (int)Math.Min(int.MaxValue, ScaleReward(amount, SoulMultiplier, ref _soulRemainder));
	private static long ScaleReward(long amount, float multiplier, ref double remainder)
	{
		if (amount <= 0) return 0;
		double total = amount * (double)multiplier + remainder;
		if (total >= long.MaxValue) { remainder = 0; return long.MaxValue; }
		long result = (long)Math.Floor(total + 0.000001);
		remainder = Math.Max(0, total - result);
		return result;
	}
	public void Tick(float delta)
	{
		if (owner.IsDead) { Clear(); return; }
		if (!float.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
		var effective = Effective().ToHashSet();
		foreach (var state in _states.ToArray())
		{
			float activeTime = Mathf.Min(delta, state.Remaining);
			state.UntilPulse -= activeTime;
			// 到期帧仅结算有效时间，卡顿补齐脉冲，不能多算一个到期后的伤害。
			while (state.UntilPulse <= 0 && activeTime > 0)
			{
				state.UntilPulse += state.Definition.PulseInterval;
				if (effective.Contains(state)) Pulse(state);
				if (owner.IsDead) return;
			}
			state.Remaining -= delta;
		}
		_states.RemoveAll(s => s.Remaining <= 0);
	}
	private void Pulse(State state)
	{
		var definition = state.Definition;
		owner.Heal(owner.MaxHealth * definition.HealRatioPerPulse + definition.HealFlatPerPulse);
		float damage = definition.DamagePerPulse + owner.MaxHealth * definition.DamageRatioPerPulse;
		if (damage > 0)
		{
			if (GodotObject.IsInstanceValid(state.SourceActor) && !state.SourceActor!.IsDead && state.SourceActor.Visible
				&& state.SourceActor.SpawnRevision == state.SourceRevision)
			{
				using var noGain = new Zaomeng.Combat.Wushuang.WushuangGain();
				using var pulseHit = new HitDefinition { FlatDamage = damage, AttackMultiplier = 0, WushuangGain = noGain,
					DamageType = definition.PulseDamageType, CanCrit = false, Knockback = Vector2.Zero, Hitstun = 0 };
				CombatResolver.Resolve(state.SourceActor, owner, pulseHit, sourceSkill: state.SourceSkill);
			}
			else owner.ReceiveHit(new(damage, Vector2.Zero, 0, definition.PulseDamageType));
		}
		if (definition.PulseHit is null || !owner.IsInsideTree()) return;
		foreach (var node in owner.GetTree().GetNodesInGroup("combat_actors"))
			if (node is CharacterActor { Visible: true, IsDead: false } target && target.Team != owner.Team
				&& owner.GlobalPosition.DistanceTo(target.GlobalPosition) <= definition.PulseRadius * state.RangeMultiplier)
				CombatResolver.Resolve(owner, target, definition.PulseHit, state.Level, state.SourceSkill);
	}
}
