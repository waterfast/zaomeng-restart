using Godot;

namespace Zaomeng;

/// <summary>技能的一次命中窗口，起始帧包含、结束帧不包含。</summary>
[GlobalClass]
public partial class HitEvent : Resource
{
    [Export] public int StartFrame { get; set; }
    [Export] public int EndFrame { get; set; }
    [Export] public HitDefinition? Hit { get; set; }
}
