using Godot;
using Zaomeng.Monsters;

namespace Zaomeng.Skills;

public partial class SummonSkillBehavior : SkillBehavior
{
	[Export] public MonsterDefinition SummonedMonster { get; set; } = null!;
	[Export] public int Count { get; set; } = 4;
	[Export] public float Delay { get; set; } = 0.4f;
	private bool _released;
	public override void ValidateConfiguration() => Require(SummonedMonster is not null && Count is > 0 and <= 10
		&& float.IsFinite(Delay) && Delay >= 0, "召唤技能配置无效。");
	protected override void OnTick(float delta)
	{
		if (_released || Elapsed < Delay) return;
		_released = true;
		if (Actor is Monster monster) monster.Summon?.Invoke(SummonedMonster, Count);
	}
}
