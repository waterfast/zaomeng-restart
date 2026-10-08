using Godot;
using Zaomeng.Combat.Buffs;

namespace Zaomeng.Skills;

public partial class BuffSkillBehavior : SkillBehavior
{
	[Export] public BuffDefinition Buff { get; set; } = null!;
	[Export] public float Delay { get; set; }
	[Export] public bool EndsWithCast { get; set; }
	private bool _applied;
	private readonly string _source = "skill:" + System.Guid.NewGuid();
	public override void ValidateConfiguration()
	{
		Require(Buff is not null && float.IsFinite(Delay) && Delay >= 0, "Buff 技能配置无效。");
		Buff!.Validate();
	}
	public override string DescribeValues(int level) => $"{Buff.Description}；持续 {Buff.Duration:0.##} 秒";
	protected override void OnTick(float delta)
	{
		if (_applied || Elapsed < Delay) return;
		_applied = true;
		Actor.Buffs.Apply(Buff, EndsWithCast ? _source : "skill:" + Buff.Id, Level, sourceSkill: Definition, rangeMultiplier: Parameters.Range);
	}
	protected override void OnStop()
	{
		if (EndsWithCast) Actor.Buffs.RemoveSource(_source);
	}
}
