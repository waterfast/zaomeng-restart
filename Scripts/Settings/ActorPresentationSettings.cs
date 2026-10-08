using Godot;

namespace Zaomeng.Settings;

/// <summary>玩家偏好只控制演员美术和血量显示，不改变战斗状态。</summary>
public partial class ActorPresentationSettings : Node2D
{
	private CharacterActor _actor = null!;
	private CanvasItem? _body;
	private CanvasItem? _equipment;

	public override void _Ready()
	{
		_actor = (CharacterActor)GetParent();
		bool player = _actor is Player;
		_body = _actor.GetNodeOrNull<CanvasItem>(player ? "Facing/Visual/RoleBody" : "Facing/Visual/Body");
		_equipment = _actor.GetNodeOrNull<CanvasItem>("Facing/Visual/RoleEquipment");
		ZIndex = 10;
		if (!player)
		{
			var healthBar = GD.Load<PackedScene>("res://Scenes/UI/Level/MonsterHealthBar.tscn").Instantiate<Zaomeng.UI.MonsterHealthBar>();
			healthBar.Bind(_actor);
			AddChild(healthBar);
		}
		GameSettings.Changed += ApplyVisibility;
		ApplyVisibility();
	}

	public override void _ExitTree() => GameSettings.Changed -= ApplyVisibility;

	private void ApplyVisibility()
	{
		if (_body is not null) _body.Modulate = new Color(_body.Modulate,
			GameSettings.IsEnabled(_actor is Player ? GameOption.PlayerBody : GameOption.MonsterBody) ? 1 : 0);
		if (_equipment is not null) _equipment.Modulate = new Color(_equipment.Modulate,
			GameSettings.IsEnabled(GameOption.PlayerEquipment) ? 1 : 0);
	}
}
