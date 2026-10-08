using Godot;

namespace Zaomeng.Skills;

public partial class HealSkillBehavior : SkillBehavior
{
	[Export] public float Delay { get; set; } = 0.7f;
	[Export] public float HealthRatio { get; set; } = 0.2f;
	[Export] public float FlatHealingPerLevel { get; set; } = 0.02f;
	[Export] public float MissingHealthMultiplier { get; set; } = 0.6f;
	[Export] public PackedScene? VisualScene { get; set; }
	[Export] public float VisualLifetime { get; set; } = 1.3f;
	private bool _healed;
	public override string DescribeValues(int level) =>
		$"基础治疗：最大生命×{HealthRatio:0.##} + {(level + 1) * FlatHealingPerLevel:0.##}（取整）；乘以（1 + 缺血比例×{MissingHealthMultiplier:0.##}）";
	public override void ValidateConfiguration() => Require(float.IsFinite(Delay) && Delay >= 0 && float.IsFinite(HealthRatio) &&
		HealthRatio is > 0 and <= 1 && float.IsFinite(FlatHealingPerLevel) && FlatHealingPerLevel >= 0 && float.IsFinite(MissingHealthMultiplier) && MissingHealthMultiplier >= 0 &&
		float.IsFinite(VisualLifetime) && VisualLifetime > 0, "治疗技能的比例或释放时间无效。");
	protected override void OnTick(float delta)
	{
		if (_healed || Elapsed < Delay) return;
		_healed = true;
		float missingRatio = 1 - Actor.Health / Actor.MaxHealth;
		// 旧版递增的是基础治疗量中的常数项，先取整再乘缺血加成，并非最大生命比例。
		float basic = Mathf.Floor(Actor.MaxHealth * HealthRatio + (Level + 1) * FlatHealingPerLevel);
		Actor.Heal(Mathf.Floor(basic * (1 + MissingHealthMultiplier * missingRatio)));
		if (VisualScene is { } scene) ActorEffectSpawner.SpawnInWorld(scene, Actor.GetParent(), Actor.GlobalPosition + new Vector2(0, -35), VisualLifetime, Parameters);
	}
}
