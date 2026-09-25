using Godot;

namespace Zaomeng;

/// <summary>普通攻击中的一段；角色按列表顺序播放，列表长度不限制。</summary>
[GlobalClass]
public partial class AttackStep : Resource
{
    // AnimationPlayer 中的动画名，以及这一段生效时使用的命中数据。
    [Export] public StringName Animation { get; set; } = "hit1";
    [Export] public HitDefinition Hit { get; set; } = null!;
    [Export] public int FramesPerSecond { get; set; } = 30;

    // 空中配置留空时沿用地面配置；两者都不填表示原地攻击。
    [Export] public ActionMotion? Motion { get; set; }
    [Export] public ActionMotion? AirMotion { get; set; }

    // 可选：动画时间轴调用 CharacterActor.SpawnAttackEffect 时生成。
    [Export] public PackedScene? EffectScene { get; set; }
    [Export] public Vector2 EffectOffset { get; set; }
    [Export] public float EffectLifetime { get; set; } = 0.5f;
}
