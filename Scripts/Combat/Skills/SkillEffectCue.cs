using System;
using Godot;

namespace Zaomeng.Skills;

/// <summary>动作时钟上的独立表现释放点；方向和挂点均由内容资源声明。</summary>
[GlobalClass]
[Tool]
public partial class SkillEffectCue : Resource
{
	[Export] public float Time { get; set; }
	[Export] public PackedScene Scene { get; set; } = null!;
	[Export] public Vector2 Offset { get; set; }
	[Export] public Vector2 Scale { get; set; } = Vector2.One;
	[Export] public bool Mirror { get; set; } = true;
	[Export] public bool FollowActor { get; set; }
	[Export] public bool EndsWithCast { get; set; }
	public void Validate()
	{
		if (!float.IsFinite(Time) || Time < 0 || Scene is null || !Offset.IsFinite() || !Scale.IsFinite()
			|| Scale.X <= 0 || Scale.Y <= 0 || (EndsWithCast && !FollowActor))
			throw new InvalidOperationException("特效释放点缺少场景或有效时间、偏移、缩放。");
		var effect = Scene.Instantiate();
		try
		{
			if (effect is not AnimatedSkillEffect animated) throw new InvalidOperationException("序列特效必须配置 AnimatedSkillEffect。");
			animated.ValidateConfiguration();
		}
		finally { effect.Free(); }
	}
}
