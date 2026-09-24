using Godot;

namespace Zaomeng;

public enum ActorState { Free, Attacking, Hurt, Dead }

public partial class CharacterActor : CharacterBody2D
{
    [Export] public int Team { get; set; }
    [Export] public float MaxHealth { get; set; } = 100;
    [Export] public float MoveSpeed { get; set; } = 220;
    [Export] public float Gravity { get; set; } = 1000;
    [Export] public float KnockbackFriction { get; set; } = 350;
    [Export] public HitDefinition NormalAttack { get; set; } = null!;
    [Export] public StringName IdleAnimation { get; set; } = "wait";
    [Export] public StringName MoveAnimation { get; set; } = "run";
    [Export] public StringName AirAnimation { get; set; } = "jump1";

    public float Health { get; private set; }
    public ActorState State { get; private set; }
    public bool IsDead => State == ActorState.Dead;
    public int FacingDirection { get; private set; } = 1;
    public CharacterMotor Motor { get; } = new();
    public AnimationPlayer Animator { get; private set; } = null!;
    public HitBox AttackBox { get; private set; } = null!;
    protected float MoveDirection;
    private Node2D _facing = null!;
    private ProgressBar _healthBar = null!;
    private float _hurtRemaining;

    public override void _Ready()
    {
        Health = MaxHealth;
        _facing = GetNode<Node2D>("Facing");
        Animator = GetNode<AnimationPlayer>("AnimationPlayer");
        AttackBox = GetNode<HitBox>("Facing/HitBox");
        _healthBar = GetNode<ProgressBar>("HealthBar");
        _healthBar.MaxValue = MaxHealth;
        _healthBar.Value = Health;
        Animator.AnimationFinished += OnAnimationFinished;
        Face(1);
        Play(IdleAnimation);
    }

    public override void _PhysicsProcess(double delta)
    {
        float step = (float)delta;
        MoveDirection = 0;
        if (State == ActorState.Hurt)
        {
            _hurtRemaining -= step;
            if (_hurtRemaining <= 0) State = ActorState.Free;
        }
        if (State == ActorState.Free) ReadIntent(step);
        if (State == ActorState.Free)
        {
            if (!Mathf.IsZeroApprox(MoveDirection)) Face(Mathf.Sign(MoveDirection));
            Play(!IsOnFloor() ? AirAnimation : Mathf.IsZeroApprox(MoveDirection) ? IdleAnimation : MoveAnimation);
        }
        Motor.Step(this, State == ActorState.Free ? MoveDirection * MoveSpeed : 0,
            Gravity, KnockbackFriction, step);
    }

    protected virtual void ReadIntent(float delta) { }

    public void Face(int direction)
    {
        if (direction == 0) return;
        FacingDirection = direction > 0 ? 1 : -1;
        // Original sheets face left; mirror visual and attack shape together.
        _facing.Scale = new(-FacingDirection, 1);
    }

    public bool TryAttack()
    {
        if (State != ActorState.Free || NormalAttack == null) return false;
        State = ActorState.Attacking;
        AttackBox.BeginAttack();
        Play("hit1");
        return true;
    }

    public void ReceiveHit(HitResult hit)
    {
        if (IsDead) return;
        Health = Mathf.Max(0, Health - hit.Damage);
        _healthBar.Value = Health;
        AttackBox.Active = false;
        Motor.ApplyKnockback(this, hit.Knockback);
        State = Health <= 0 ? ActorState.Dead : ActorState.Hurt;
        _hurtRemaining = hit.Hitstun;
        Play(IsDead ? "death" : "hurt", restart: true);
    }

    private void OnAnimationFinished(StringName animation)
    {
        if (State == ActorState.Attacking && animation == "hit1")
        {
            AttackBox.Active = false;
            State = ActorState.Free;
        }
    }

    private void Play(StringName animation, bool restart = false)
    {
        if (!restart && Animator.CurrentAnimation == animation && Animator.IsPlaying()) return;
        Animator.Play(animation);
        // Apply frame-zero reset tracks immediately when an attack is interrupted.
        Animator.Advance(0);
    }
}
