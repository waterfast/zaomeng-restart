using Godot;

namespace Zaomeng;

public enum ActionMotionMode
{
	Stop,
	CarryMomentum,
	FacingDash
}

/// <summary>一次普攻或技能的水平位移配置；帧号从 0 开始，结束帧不包含在区间内。</summary>
[GlobalClass]
public partial class ActionMotion : Resource
{
	[Export] public ActionMotionMode Mode { get; set; } = ActionMotionMode.Stop;
	[Export] public int StartFrame { get; set; }
	[Export] public int EndFrame { get; set; } = -1; // -1 表示持续到动画结束。
	[Export] public float Speed { get; set; } // 冲刺速度，方向由起招时的面向决定。
	[Export] public float MomentumDeceleration { get; set; } // 0 表示完整保留起招惯性。

	public bool IsValidFor(double animationLength, int framesPerSecond)
	{
		if (framesPerSecond <= 0 || StartFrame < 0 || EndFrame < -1
			|| (EndFrame >= 0 && EndFrame <= StartFrame)
			|| StartFrame / (double)framesPerSecond >= animationLength)
			return false;

		return Mode switch
		{
			ActionMotionMode.CarryMomentum => MomentumDeceleration >= 0,
			ActionMotionMode.FacingDash => Speed > 0,
			_ => true
		};
	}
}
