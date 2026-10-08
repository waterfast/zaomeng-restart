using System;
using Godot;

namespace Zaomeng.Combat.Wushuang;

/// <summary>攻击接触一次目标时获取的整数闭区间；零区间明确表示不获取。</summary>
[GlobalClass]
[Tool]
public partial class WushuangGain : Resource
{
	[Export] public int Minimum { get; set; }
	[Export] public int Maximum { get; set; }
	public void Validate()
	{
		if (Minimum < 0 || Maximum < Minimum)
			throw new InvalidOperationException("无双获取区间必须非负，且最大值不能小于最小值。");
	}
	public int Roll()
	{
		Validate();
		return Minimum == Maximum ? Minimum : GD.RandRange(Minimum, Maximum);
	}
	public static int Resolve(HitDefinition hit, SkillDefinition? skill)
		=> (hit.WushuangGain ?? skill?.WushuangGain)?.Roll() ?? 2;
}
