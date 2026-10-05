using Godot;

namespace Zaomeng.Equipment;

/// <summary>仅用于旧法宝详情展示，不授予战斗行为或持有成长状态。</summary>
[GlobalClass]
[Tool]
public partial class MagicWeaponPresentation : Resource
{
	[Export] public StringName Animation { get; set; } = "";
	[Export] public string SkillName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string SkillDescription { get; set; } = "";
	[Export] public Texture2D? SkillIcon { get; set; }
	[Export(PropertyHint.MultilineText)] public string PassiveDescription { get; set; } = "暂无";
}
