using System.Collections.Generic;
using Godot;

namespace Zaomeng.Level.Traps;

/// <summary>预警后伸出地刺；同一次伸出无论停留或重入均只命中一次。</summary>
public partial class GroundSpikeTrap : Node2D
{
	[Export] public TrapDefinition Definition { get; set; } = null!;
	[Export] public float PhaseOffset { get; set; }
	private readonly HashSet<Player> _hit = new();
	private float _clock;
	private bool _active;
	private bool _grounded;
	private Sprite2D _sprite = null!;
	private Area2D _damageArea = null!;
	public bool IsActive => _active;
	public override void _Ready()
	{
		Definition.Validate();
		_sprite = GetNode<Sprite2D>("Visual");
		_damageArea = GetNode<Area2D>("DamageArea");
		_clock = PhaseOffset;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!_grounded)
		{
			var query = PhysicsRayQueryParameters2D.Create(new(GlobalPosition.X, -200), new(GlobalPosition.X, 900), 1);
			var ground = GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (ground.Count == 0) { GD.PushError($"地刺 {Name} 下方没有地形。"); SetPhysicsProcess(false); return; }
			GlobalPosition = ground["position"].AsVector2();
			_grounded = true;
		}
		float cycle = Definition.RestSeconds + Definition.WarningSeconds + Definition.ActiveSeconds;
		_clock = (_clock + (float)delta) % cycle;
		bool active = _clock >= Definition.RestSeconds + Definition.WarningSeconds;
		if (active && !_active) _hit.Clear();
		_active = active;
		_sprite.Visible = _clock >= Definition.RestSeconds;
		_sprite.Modulate = active ? Colors.White : new Color(1, 0.4f, 0.2f, 0.45f);
		_sprite.Scale = new(1, active ? 1 : 0.3f);
		_sprite.Position = new(0, active ? -24 : -7);
		if (!active) return;
		foreach (var node in _damageArea.GetOverlappingBodies())
			if (node is Player { IsDead: false } player && !player.IsInvulnerable && !_hit.Contains(player))
			{
				_hit.Add(player);
				player.ReceiveHit(new(Definition.Damage, new(15 * Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X), -15), 0.2f, DamageType.True));
			}
	}
}
