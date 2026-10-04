using System.Globalization;
using Godot;

namespace Zaomeng;

/// <summary>用旧版数字贴图组装伤害值，播放迁入的原版动画轨道。</summary>
public partial class LegacyDamageText : Node2D
{
	private HitResult _hit;
	private bool _playerWasHit;

	public void Configure(HitResult hit, bool playerWasHit)
	{
		_hit = hit;
		_playerWasHit = playerWasHit;
	}

	public override void _Ready()
	{
		var digits = GetNode<HBoxContainer>("Midlle/Number");
		string type = _hit.DamageType switch
		{
			DamageType.Magic => "magic", DamageType.True => "real", _ => "physics"
		};
		// 武力用红色、魔法用蓝色；旧版只有怪物受暴击使用大号图，玩家靠抖动动画体现。
		string folder = type == "real" ? "real" : _hit.Critical && !_playerWasHit ? $"{type}crit"
			: type == "physics" ? "monster/physics" : "magic";
		if (type == "physics") digits.AddThemeConstantOverride("separation", _hit.Critical ? -20 : -10);
		string value = ((int)Mathf.Max(0, _hit.Damage)).ToString(CultureInfo.InvariantCulture);
		foreach (char digit in value)
		{
			digits.AddChild(new TextureRect
			{
				Texture = GD.Load<Texture2D>($"res://Assets/Art/AllNumber/{folder}/{type}_{digit}.png"),
				TextureFilter = TextureFilterEnum.Linear,
				StretchMode = TextureRect.StretchModeEnum.Keep,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
		}
		// 原版只有玩家受到暴击时播放彩色抖动；怪物暴击靠数字贴图体现。
		GetNode<AnimationPlayer>("ShowPlayer").Play(_playerWasHit && _hit.Critical ? "Crit" : "physics");
	}
}
