using System;
using System.Collections.Generic;
using Godot;

namespace Zaomeng.Combat.Wushuang;

/// <summary>只管理无双资源与开启状态；战斗增益交由 Buff 系统管理。</summary>
public sealed class WushuangRuntime(Player owner)
{
	public const int Maximum = 100;
	private const string BuffSource = "wushuang";
	private double _clock;
	private ulong _sampleFrame = ulong.MaxValue;
	private readonly Dictionary<(HitDefinition Hit, SkillDefinition? Skill, WushuangGain? Gain), int> _samples = new();
	public int Value { get; private set; }
	public bool IsActive { get; private set; }
	public bool IsReady => !IsActive && !owner.IsDead && Value == Maximum;
	public int SampleGain(HitDefinition hit, SkillDefinition? skill)
	{
		ulong frame = Engine.GetPhysicsFrames();
		if (_sampleFrame != frame) { _sampleFrame = frame; _samples.Clear(); }
		var key = (hit, skill, hit.WushuangGain ?? skill?.WushuangGain);
		// 旧角色每帧更新攻击字典，同一帧同攻击命中多个目标使用同一个随机值。
		if (!_samples.TryGetValue(key, out int amount))
			_samples[key] = amount = WushuangGain.Resolve(hit, skill);
		return amount;
	}
	public void Gain(int amount)
	{
		if (owner.IsDead || IsActive || amount <= 0) return;
		Value = (int)Math.Min(Maximum, (long)Value + amount);
	}
	public bool TryActivate()
	{
		if (!IsReady || owner.Buffs.PreventsActions || owner.WushuangBuff is not { } buff) return false;
		buff.Validate();
		owner.Buffs.Apply(buff, BuffSource, permanent: true);
		IsActive = true;
		owner.PassiveEffects.RefreshBuffSources();
		return true;
	}
	public void Tick(double delta)
	{
		if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
		if (owner.IsDead) { Reset(); return; }
		_clock += delta;
		while (_clock + 0.0000001 >= 0.5)
		{
			_clock -= 0.5;
			// 旧值为 int：减 0.2 后赋回整数会截断，未满实际每跳减 1。
			if (IsActive) Value = Math.Max(0, Value - 4);
			else if (Value < Maximum) Value = Math.Max(0, Value - 1);
			if (IsActive && Value == 0) End();
		}
	}
	private void End()
	{
		IsActive = false;
		owner.Buffs.RemoveSource(BuffSource);
		owner.PassiveEffects.RefreshBuffSources();
	}
	public void Reset()
	{
		Value = 0;
		_clock = 0;
		_samples.Clear();
		_sampleFrame = ulong.MaxValue;
		if (IsActive) End();
		else owner.Buffs.RemoveSource(BuffSource);
	}
}
