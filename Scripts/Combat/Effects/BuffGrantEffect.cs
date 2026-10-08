using Godot;
using Zaomeng.Combat.Buffs;

namespace Zaomeng.Combat.Effects;

[GlobalClass]
[Tool]
public partial class BuffGrantEffect : PassiveEffect
{
	[Export] public BuffDefinition Buff { get; set; } = null!;
	[Export] public bool OnlyDuringWushuang { get; set; }
	public override void Validate()
	{
		if (Buff is null) throw new System.InvalidOperationException("被动缺少 Buff 资源。");
		Buff.Validate();
	}
}
