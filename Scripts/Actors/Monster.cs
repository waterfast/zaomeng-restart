using Godot;

namespace Zaomeng;

public partial class Monster : CharacterActor
{
    [Export] public NodePath TargetPath { get; set; } = new("../Player");
    [Export] public float AttackRange { get; set; } = 58;
    [Export] public float DetectionRange { get; set; } = 550;
    [Export] public float AttackCooldown { get; set; } = 1.1f;
    [Export] public bool AiEnabled { get; set; } = true;
    private CharacterActor? _target;
    private float _cooldown;

    public override void _Ready()
    {
        base._Ready();
        _target = GetNodeOrNull<CharacterActor>(TargetPath);
    }

    protected override void ReadIntent(float delta)
    {
        _cooldown = Mathf.Max(0, _cooldown - delta);
        if (!AiEnabled || !IsInstanceValid(_target) || _target!.IsDead) return;
        Vector2 offset = _target.GlobalPosition - GlobalPosition;
        if (offset.Length() > DetectionRange) return;
        Face(Mathf.Sign(offset.X));
        if (Mathf.Abs(offset.X) > AttackRange) MoveDirection = Mathf.Sign(offset.X);
        else if (_cooldown <= 0 && Mathf.Abs(offset.Y) < 65 && TryAttack()) _cooldown = AttackCooldown;
    }
}
