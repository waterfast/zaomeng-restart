using Godot;

namespace Zaomeng;

/// <summary>技能的静态动作和命中数据；帧号从 0 开始。</summary>
[GlobalClass]
public partial class SkillDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public StringName Animation { get; set; } = "";
    [Export] public int FramesPerSecond { get; set; } = 30;
    // 旧版冷却和蓝耗作为静态策划数据保存；运行态由玩家管理。
    [Export] public float CooldownSeconds { get; set; }
    [Export] public int BaseManaCost { get; set; }
    [Export] public int ManaCostLinearGrowth { get; set; }
    [Export] public int ManaCostQuadraticGrowth { get; set; }

    public int GetManaCost(int level)
    {
        int extraLevels = Mathf.Max(0, level - 1);
        return BaseManaCost + extraLevels * ManaCostLinearGrowth
            + extraLevels * extraLevels * ManaCostQuadraticGrowth;
    }

    // 位移只描述角色本身，不影响特效场景和命中时间。
    [Export] public ActionMotion? Motion { get; set; }
    [Export] public ActionMotion? AirMotion { get; set; }

    // 每个窗口可命中同一目标一次；多段技能按窗口分别结算。
    [Export] public Godot.Collections.Array<HitEvent> Hits { get; set; } = new();

    // 特效可由场景脚本实现弹道等行为；0 秒寿命表示由特效自行销毁。
    [Export] public PackedScene? EffectScene { get; set; }
    [Export] public int EffectFrame { get; set; }
    [Export] public Vector2 EffectOffset { get; set; }
    [Export] public float EffectLifetime { get; set; } = 0.5f;
    [Export] public bool EffectFollowsActor { get; set; } = true;
}
