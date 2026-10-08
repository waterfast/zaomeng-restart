using System;
using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng.Skills;

/// <summary>有限时长的范围爆发配置，角色动作与独立法宝共用。</summary>
[GlobalClass]
[Tool]
public partial class BurstAttackDefinition : Resource
{
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public StringName Animation { get; set; } = "";
	[Export] public float Radius { get; set; } = 75;
	[Export] public Vector2 VisualOffset { get; set; }
	[Export] public bool AlignToGround { get; set; } = true;
	[Export] public Vector2[] Positions { get; set; } = [Vector2.Zero];
	[Export] public float[] ReleaseTimes { get; set; } = [0];
	[Export] public float[] Scales { get; set; } = [1];
	[Export] public Godot.Collections.Array<HitEvent> Hits { get; set; } = new();
	public void Validate()
	{
		if (Frames is null || !Frames.HasAnimation(Animation) || Frames.GetFrameCount(Animation) == 0 ||
			Frames.GetAnimationLoopMode(Animation) != SpriteFrames.LoopMode.None ||
			!double.IsFinite(Frames.GetAnimationSpeed(Animation)) || Frames.GetAnimationSpeed(Animation) <= 0 ||
			!float.IsFinite(Radius) || Radius <= 0 || !VisualOffset.IsFinite() || Positions.Length == 0 ||
			ReleaseTimes.Length != Positions.Length || Scales.Length != Positions.Length || Hits.Count == 0)
			throw new InvalidOperationException("范围爆发缺少有效帧图、时序或命中配置。");
		for (int i = 0; i < Positions.Length; i++)
			if (!Positions[i].IsFinite() || !float.IsFinite(ReleaseTimes[i]) || ReleaseTimes[i] < 0 ||
				(i > 0 && ReleaseTimes[i] < ReleaseTimes[i - 1]) || !float.IsFinite(Scales[i]) || Scales[i] <= 0)
				throw new InvalidOperationException("范围爆发释放点必须按时间排列，缩放必须为正。");
		int previousEnd = 0;
		foreach (HitEvent window in Hits)
		{
			if (window?.Hit is null || window.StartFrame < previousEnd || window.EndFrame <= window.StartFrame ||
				window.EndFrame > Frames.GetFrameCount(Animation))
				throw new InvalidOperationException("范围爆发命中窗口无效。");
			previousEnd = window.EndFrame;
		}
	}
	public void Release(CharacterActor actor, SkillDefinition skill, int level, int index, Vector2 origin, int direction, AttackParameters parameters)
	{
		var effect = new AreaAttackEffect { AlignVisualToGround = AlignToGround };
		effect.Configure(actor, skill, level, Radius * Scales[index], Hits, Frames, Animation,
			new Vector2(-direction * Scales[index], Scales[index]), VisualOffset, parameters);
		actor.GetParent().AddChild(effect);
		effect.GlobalPosition = origin + new Vector2(Positions[index].X * direction, Positions[index].Y);
	}
}
