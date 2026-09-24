using Godot;

namespace Zaomeng;

public partial class Player : CharacterActor
{
    [Export] public float JumpSpeed { get; set; } = 430;
    public bool InputEnabled { get; set; } = true;

    protected override void ReadIntent(float delta)
    {
        if (!InputEnabled) return;
        MoveDirection = Input.GetAxis("move_left", "move_right");
        if (!Mathf.IsZeroApprox(MoveDirection)) Face(Mathf.Sign(MoveDirection));
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            Velocity = new(Velocity.X, -JumpSpeed);
        if (Input.IsActionJustPressed("attack")) TryAttack();
    }
}
