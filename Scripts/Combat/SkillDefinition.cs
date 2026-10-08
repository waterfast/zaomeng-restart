using Godot;

namespace Zaomeng;

/// <summary>技能的静态动作和命中数据；帧号从 0 开始。</summary>
[GlobalClass]
[Tool]
public partial class SkillDefinition : Resource
{
	[Export] public AudioStream? Sound { get; set; }
	[Export] public string Id { get; set; } = "";
	[Export] public Zaomeng.Combat.Wushuang.WushuangGain? WushuangGain { get; set; }
	[Export] public string DisplayName { get; set; } = "";
	[Export] public StringName Animation { get; set; } = "";
	[Export] public int FramesPerSecond { get; set; } = 30;
	// 旧版冷却和蓝耗作为静态策划数据保存；运行态由玩家管理。
	[Export] public float CooldownSeconds { get; set; }
	// 延迟冷却由行为触发；时长仍在施放时按极速快照。
	[Export] public bool StartCooldownOnImpact { get; set; }
	[Export] public Zaomeng.Skills.SkillGrowth Growth { get; set; } = new();
	[Export] public bool Invulnerable { get; set; }
	[Export] public PackedScene? BehaviorScene { get; set; }
	public int GetManaCost(int level) => Growth.ManaCost(level);

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
