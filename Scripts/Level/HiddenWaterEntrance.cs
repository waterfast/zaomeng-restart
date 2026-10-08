using System;
using Godot;

namespace Zaomeng.Level;

/// <summary>水面输入与下潜表现；关卡切换及库存保存交给关卡装配器。</summary>
public partial class HiddenWaterEntrance : Node
{
	[Export] public NodePath[] SurfacePaths { get; set; } = [];
	[Export] public LevelDefinition LowerLevel { get; set; } = null!;
	[Export] public LevelDefinition UpperLevel { get; set; } = null!;
	[Export] public int UpperLevelThreshold { get; set; } = 21;
	[Export] public float DiveDepth { get; set; } = 95;
	private Player _player = null!;
	private GameplayLevel _level = null!;
	private CollisionShape2D[] _surfaces = [];
	private CollisionShape2D? _active;
	private Vector2 _start;
	public bool IsDiving => _active is not null;
	public LevelDefinition DestinationFor(int playerLevel) => playerLevel < UpperLevelThreshold ? LowerLevel : UpperLevel;
	public override void _Ready()
	{
		// 地图可单独用于地形巡检和预览，没有关卡装配器时不绑定游戏输入。
		if (GetParent().GetParent() is not GameplayLevel level) { SetPhysicsProcess(false); return; }
		_level = level;
		if (LowerLevel is null || UpperLevel is null || SurfacePaths.Length == 0
			|| UpperLevelThreshold < 1 || !float.IsFinite(DiveDepth) || DiveDepth <= 0)
			throw new InvalidOperationException("隐藏水面入口配置无效。");
		_player = _level.GetNode<Player>("Player");
		_surfaces = Array.ConvertAll(SurfacePaths, path => GetNode<CollisionShape2D>(path));
		_player.TryDropThrough = TryBeginDive;
	}
	public bool TryBeginDive()
	{
		if (IsDiving || !_player.InputEnabled || _player.IsDead || !_player.IsOnFloor()
			|| _player.State != ActorState.Free || _player.Buffs.PreventsActions) return false;
		foreach (var surface in _surfaces)
		{
			if (surface.Disabled || surface.Shape is not RectangleShape2D rectangle) continue;
			Vector2 local = surface.ToLocal(_player.GlobalPosition);
			if (Mathf.Abs(local.X) > rectangle.Size.X / 2 - 8 || Mathf.Abs(local.Y + rectangle.Size.Y / 2) > 8) continue;
			var destination = DestinationFor(_player.Level);
			if (destination is null || !ResourceLoader.Exists(destination.LevelScenePath)) return false;
			_active = surface;
			_start = _player.GlobalPosition;
			surface.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			_player.InputEnabled = false;
			_player.Velocity = new(0, 90);
			return true;
		}
		return false;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsDiving) return;
		if (_player.IsDead) { CancelDive(); return; }
		if (_player.GlobalPosition.Y < _start.Y + DiveDepth) return;
		if (!_level.EnterHiddenLevel(DestinationFor(_player.Level)))
		{
			_player.GlobalPosition = _start + new Vector2(0, -4);
			_player.Velocity = Vector2.Zero;
			CancelDive();
		}
	}
	private void CancelDive()
	{
		if (IsInstanceValid(_active)) _active!.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		_active = null;
		if (IsInstanceValid(_player) && !_player.IsDead) _player.InputEnabled = true;
	}
	public override void _ExitTree()
	{
		CancelDive();
		if (IsInstanceValid(_player)) _player.TryDropThrough = null;
	}
}
