using System.Collections.Generic;
using Godot;

namespace Zaomeng.Skills;

/// <summary>按动作时钟释放数据指定的特效，世界效果有自己的生命周期。</summary>
public partial class SkillEffectSequenceBehavior : SkillBehavior
{
	[Export] public Godot.Collections.Array<SkillEffectCue> Cues { get; set; } = new();
	private int _released;
	private readonly List<AnimatedSkillEffect> _castEffects = new();
	public override void ValidateConfiguration()
	{
		Require(Cues.Count > 0, "特效序列不能为空。");
		float previous = -1;
		foreach (var cue in Cues)
		{
			Require(cue is not null && cue.Time >= previous, "特效释放点必须按时间排列。");
			cue!.Validate();
			previous = cue.Time;
		}
	}
	public override string DescribeValues(int level)
	{
		var values = new List<string>();
		var scenes = new HashSet<PackedScene>();
		foreach (var cue in Cues)
		{
			if (!scenes.Add(cue.Scene)) continue;
			var effect = cue.Scene.Instantiate<AnimatedSkillEffect>();
			try
			{
				if (effect.Hit is { } hit) values.Add($"每段倍率 {hit.MinimumMultiplier(level):0.###}，{effect.HitTimes.Length}段");
				if (effect.HealRatio > 0) values.Add($"每{effect.HealInterval:0.##}秒恢复{effect.HealRatio:P1}最大生命，持续{effect.Lifetime:0.##}秒");
			}
			finally { effect.Free(); }
		}
		return values.Count > 0 ? string.Join("；", values) : "独立表现效果";
	}
	protected override void OnTick(float delta)
	{
		while (_released < Cues.Count && Elapsed >= Cues[_released].Time)
		{
			var cue = Cues[_released++];
			var effect = cue.Scene.Instantiate<AnimatedSkillEffect>();
			effect.Configure(Actor, Definition, Level, Parameters, cue);
			Actor.GetParent().AddChild(effect);
			if (cue.EndsWithCast) _castEffects.Add(effect);
		}
	}
	protected override void OnStop()
	{
		foreach (var effect in _castEffects)
			if (IsInstanceValid(effect)) effect.QueueFree();
	}
}
