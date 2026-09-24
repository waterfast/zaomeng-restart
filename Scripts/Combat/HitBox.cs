using Godot;
using System.Collections.Generic;

namespace Zaomeng;

public partial class HitBox : Area2D
{
    // Keyed by AnimationPlayer. Keep monitoring so targets already inside are hit too.
    [Export] public bool Active { get; set; }
    private CharacterActor _actor = null!;
    private readonly HashSet<ulong> _hitTargets = new();

    public override void _Ready()
    {
        _actor = GetParent().GetParent<CharacterActor>();
        AreaEntered += TryHit;
    }

    public void BeginAttack()
    {
        Active = false;
        _hitTargets.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active) return;
        foreach (var area in GetOverlappingAreas()) TryHit(area);
    }

    private void TryHit(Area2D area)
    {
        if (!Active || _actor.State != ActorState.Attacking || area is not HurtBox hurtBox)
            return;
        var target = hurtBox.Actor;
        if (_hitTargets.Contains(target.GetInstanceId())) return;
        if (CombatResolver.Resolve(_actor, target, _actor.NormalAttack))
            _hitTargets.Add(target.GetInstanceId());
    }
}
