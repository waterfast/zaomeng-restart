using System;
using Godot;

namespace Zaomeng.Level;

/// <summary>通关光圈只处理范围与交互，关卡负责提交结果和切换界面。</summary>
public partial class LevelExit : AnimatedSprite2D
{
	[Export] public float InteractionRadius { get; set; } = 85;
	public event Action? Activated;
	private Player _player = null!;
	private bool _activated;
	public void Bind(Player player) => _player = player;
	public bool CanActivate => !_activated && !GetTree().Paused && IsInstanceValid(_player) &&
		!_player.IsDead && _player.InputEnabled &&
		GlobalPosition.DistanceTo(_player.GlobalPosition) <= InteractionRadius;
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
