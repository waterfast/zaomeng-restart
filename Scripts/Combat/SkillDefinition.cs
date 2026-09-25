using Godot;

namespace Zaomeng;

/// <summary>一个技能的动画、命中时间和释放特效；帧号从 0 开始。</summary>
[GlobalClass]
public partial class SkillDefinition : Resource
{
    [Export] public StringName Animation { get; set; } = "";
    [Export] public int FramesPerSecond { get; set; } = 30;

    // 位移只描述角色本身，不影响特效场景和命中时间。
    [Export] public ActionMotion? Motion { get; set; }
    [Export] public ActionMotion? AirMotion { get; set; }

    // Hit 为空时，此技能不使用角色身上的攻击框。
    [Export] public HitDefinition? Hit { get; set; }
    [Export] public int HitStartFrame { get; set; } = -1;
    [Export] public int HitEndFrame { get; set; } = -1; // 此帧起关闭攻击框。

    // 特效可由场景脚本实现弹道等行为；0 秒寿命表示由特效自行销毁。
    [Export] public PackedScene? EffectScene { get; set; }
    [Export] public int EffectFrame { get; set; }
    [Export] public Vector2 EffectOffset { get; set; }
    [Export] public float EffectLifetime { get; set; } = 0.5f;
    [Export] public bool EffectFollowsActor { get; set; } = true;
}
