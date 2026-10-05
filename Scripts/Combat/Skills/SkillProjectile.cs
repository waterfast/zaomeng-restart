using System.Collections.Generic;
using Godot;

namespace Zaomeng.Skills;

/// <summary>脱离施法者后独立运行的弹体，穿过怪物时只对每个目标结算一次。</summary>
public partial class SkillProjectile : Node2D
{
	[Export] public float Speed { get; set; } = 600;
	[Export] public float Lifetime { get; set; } = 1.4f;
	[Export] public float HitRadius { get; set; } = 55;
	[Export] public HitDefinition Hit { get; set; } = null!;
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public StringName Animation { get; set; } = "";
	[Export] public float VisualScale { get; set; } = 1;
	private Player? _source;
	private int _direction;
	private int _level;
	private SkillDefinition? _sourceSkill;
	private float _elapsed;
	private readonly HashSet<Monster> _hitTargets = new();
	public void Configure(Player source, int? level = null, SkillDefinition? sourceSkill = null)
	{
		_source = source; _direction = source.FacingDirection; _level = level ?? source.CurrentHitLevel;
		_sourceSkill = sourceSkill ?? source.CurrentSkill;
	}
	public override void _Ready()
	{
		if (_source is null)
		{
			for (Node? ancestor = GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
				if (ancestor is Player player) { Configure(player); break; }
			if (_source is null) { QueueFree(); return; }
			CallDeferred(MethodName.DetachFromActor);
		}
		var sprite = new AnimatedSprite2D { SpriteFrames = Frames, Scale = new(-_direction * VisualScale, VisualScale) };
		AddChild(sprite);
		sprite.Play(Animation);
	}
	private void DetachFromActor()
	{
		if (!IsInstanceValid(_source)) { QueueFree(); return; }
		Reparent(_source!.GetParent(), true);
		Scale = Vector2.One;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_source) || _source!.IsDead) { QueueFree(); return; }
		_elapsed += (float)delta;
		if (_elapsed >= Lifetime) { QueueFree(); return; }
		Vector2 destination = GlobalPosition + new Vector2(_direction * Speed * (float)delta, 0);
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, destination, 1);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0) { QueueFree(); return; }
		GlobalPosition = destination;
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
			if (node is Monster { Visible: true } monster && !monster.IsDead && !_hitTargets.Contains(monster) &&
				Mathf.Abs(monster.GlobalPosition.X - GlobalPosition.X) <= HitRadius &&
				Mathf.Abs(monster.GlobalPosition.Y - 35 - GlobalPosition.Y) <= 65 && CombatResolver.Resolve(_source, monster, Hit, _level, _sourceSkill))
				_hitTargets.Add(monster);
	}
}
