using System;
using Godot;
using Zaomeng.Character;

namespace Zaomeng.Skills;

public enum PassiveStat { CriticalRating, HealthRegeneration, ManaRegeneration, LifeSteal, Haste }

[GlobalClass]
public partial class PassiveSkillBonus : Resource
{
	[Export] public PassiveStat Stat { get; set; }
	[Export] public float[] Values { get; set; } = [];
	public void Validate(int maximumLevel)
	{
		if (!Enum.IsDefined(Stat) || Values.Length != maximumLevel || Array.Exists(Values, value => !float.IsFinite(value) || value < 0))
			throw new InvalidOperationException("被动属性或逐级加成表无效。");
	}
	public float ValueAtLevel(int level) => Values[level - 1];
	public void AddTo(CharacterStats total, int level)
	{
		float value = ValueAtLevel(level);
		switch (Stat)
		{
			case PassiveStat.CriticalRating: total.CriticalRating += value; break;
			case PassiveStat.HealthRegeneration: total.HealthRegeneration += value; break;
			case PassiveStat.ManaRegeneration: total.ManaRegeneration += value; break;
			case PassiveStat.LifeSteal: total.LifeSteal += value; break;
			case PassiveStat.Haste: total.HasteRating += value; break;
		}
	}
}
