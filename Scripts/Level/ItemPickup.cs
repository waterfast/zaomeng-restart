using System;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Items;
using Zaomeng.Settings;

namespace Zaomeng.Level;

/// <summary>地面物品只负责表现与接近检测；背包和存档由关卡提交。</summary>
public partial class ItemPickup : CharacterBody2D
{
	public ItemDefinition Item { get; private set; } = null!;
	public int Count { get; private set; }
	public bool Collected { get; private set; }
	private Player _player = null!;
	private Func<ItemPickup, bool> _collect = null!;
	private Node2D _visual = null!;
	private Label _hint = null!;
	private Label _name = null!;
	private float _elapsed;
	private float _groundElapsed;
	private float _retryDelay;
	private Color _qualityColor;

	public void Configure(ItemDefinition item, int count, Player player, Func<ItemPickup, bool> collect)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
		Item = item;
		Count = count;
		_player = player;
		_collect = collect;
	}

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_hint = GetNode<Label>("Visual/Hint");
		_qualityColor = Item is EquipmentDefinition equipment ? equipment.QualityColor : Colors.White;
		var icon = GetNode<Sprite2D>("Visual/Icon");
		icon.Texture = Item.GroundIcon ?? Item.Icon;
		if (icon.Texture is { } texture)
			icon.Scale = Vector2.One * (40f / Mathf.Max(texture.GetWidth(), texture.GetHeight()));
		_name = GetNode<Label>("Visual/Name");
		_name.Text = Count > 1 ? $"{Item.DisplayName} ×{Count}" : Item.DisplayName;
		_name.AddThemeColorOverride("font_color", _qualityColor);
		_name.Hide();
		QueueRedraw();
	}

	public override void _Draw()
	{
		// 光晕与名称保证小尺寸装备在深浅地形上都容易辨认。
		DrawCircle(new Vector2(0, -22), 25, new Color(_qualityColor, 0.14f));
		DrawArc(new Vector2(0, -22), 23, 0, Mathf.Tau, 32, new Color(_qualityColor, 0.7f), 2, true);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Collected) return;
		float seconds = (float)delta;
		if (Item is RecoveryPickupDefinition recovery && _elapsed + seconds >= recovery.LifetimeSeconds)
		{ QueueFree(); return; }
		_elapsed += seconds;
		_retryDelay = Mathf.Max(0, _retryDelay - seconds);
		Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0, 160 * seconds),
			IsOnFloor() ? 0 : Mathf.Min(Velocity.Y + 980 * seconds, 700));
		MoveAndSlide();
		if (IsOnFloor()) _groundElapsed += seconds;
		else _groundElapsed = 0;
		if (!IsInstanceValid(_player) || !_player.IsInsideTree() || _player.IsDead) return;
		bool nearby = Mathf.Abs(_player.GlobalPosition.X - GlobalPosition.X) <= 38
			&& Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y) <= 45;
		bool showName = IsNearestToPlayer();
		_name.Visible = showName;
		if (!nearby || !showName) _hint.Hide();
		// 先完成掉落显现，避免贴身击杀时图标还没出现就被领取。
		bool autoCollect = GameSettings.AutomaticPickup && _groundElapsed >= 3;
		if (_elapsed < 0.6f || !IsOnFloor() || (!nearby && !autoCollect) || _retryDelay > 0) return;
		if (!_collect(this))
		{
			_hint.Visible = showName;
			_retryDelay = 0.75f;
			return;
		}
		Collected = true;
		Zaomeng.Audio.AudioManager.Instance?.PlayPickup();
		_hint.Hide();
		SetPhysicsProcess(false);
		CollisionMask = 0;
		Tween disappear = CreateTween().SetParallel();
		disappear.TweenProperty(_visual, "position:y", -40f, 0.3);
		disappear.TweenProperty(this, "modulate:a", 0f, 0.3);
		disappear.Chain().TweenCallback(Callable.From(QueueFree));
	}

	private bool IsNearestToPlayer()
	{
		float distance = GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
		if (distance > 120 * 120) return false;
		// 密集掉落时只展示最近一件的文字，避免名称与失败提示互相覆盖。
		foreach (Node child in GetParent().GetChildren())
		{
			if (child is not ItemPickup other || other == this || other.Collected) continue;
			float otherDistance = other.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
			if (otherDistance < distance || (Mathf.IsEqualApprox(otherDistance, distance)
				&& other.GetIndex() < GetIndex())) return false;
		}
		return true;
	}
}
