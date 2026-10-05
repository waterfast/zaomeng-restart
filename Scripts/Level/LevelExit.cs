using System;
using Godot;

namespace Zaomeng.Level;

/// <summary>通关光圈只处理范围与交互，关卡负责提交结果和切换界面。</summary>
public partial class LevelExit : AnimatedSprite2D
{
	[Export] public float InteractionRadius { get; set; } = 85;
	public event Action? Activated;
	private Player _player = null!;
	private Label _prompt = null!;
	private bool _activated;
	public void Bind(Player player) => _player = player;
	public bool CanActivate => !_activated && !GetTree().Paused && IsInstanceValid(_player) &&
		!_player.IsDead && _player.InputEnabled &&
		GlobalPosition.DistanceTo(_player.GlobalPosition + new Vector2(0, -45)) <= InteractionRadius;
	public override void _Ready()
	{
		_prompt = new Label { Position = new(-140, -130), Size = new(280, 36), HorizontalAlignment = HorizontalAlignment.Center };
		_prompt.AddThemeFontOverride("font", GD.Load<FontFile>("res://Assets/Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf"));
		_prompt.AddThemeFontSizeOverride("font_size", 24);
		_prompt.AddThemeConstantOverride("outline_size", 4);
		_prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		AddChild(_prompt);
	}
	public override void _Process(double delta) => _prompt.Text = CanActivate ? "按 W 结算" : "靠近光圈，按 W 结算";
	public override void _Input(InputEvent input)
	{
		if (input is not InputEventKey { Pressed: true, Echo: false } key ||
			(key.PhysicalKeycode != Key.W && key.Keycode != Key.W) || !CanActivate) return;
		_activated = true;
		// 输入阶段即关闭战斗输入，阻止同一按键在物理帧再触发角色技能。
		_player.InputEnabled = false;
		Activated?.Invoke();
		GetViewport().SetInputAsHandled();
	}
}
