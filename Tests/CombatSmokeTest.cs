using Godot;
using System;
using System.Threading.Tasks;

namespace Zaomeng;

/// <summary>Engine integration checks, run with -- --smoke-test.</summary>
public partial class CombatSmokeTest : Node
{
    public override async void _Ready()
    {
        try
        {
            var arena = GetParent();
            var player = arena.GetNode<Player>("Player");
            var monster = arena.GetNode<Monster>("Monster");
            monster.AiEnabled = false;
            player.InputEnabled = false;
            player.Position = new(400, 490);
            monster.Position = new(470, 490);
            await Frames(20);
            Check(player.IsOnFloor() && monster.IsOnFloor(), "original map floor collision");
            Check(Math.Abs(player.Animator.GetAnimation("hit1").Length - 0.35) < 0.001, "0.35 second attack timeline");
            Check(player.TryAttack(), "attack starts");
            float initialX = monster.Position.X;
            bool sawKnockback = false;
            for (int i = 0; i < 28; i++)
            {
                await Frames(1);
                sawKnockback |= monster.Motor.ExternalVelocityX > 0;
            }
            Check(monster.Health == monster.MaxHealth - 12, "one hit per swing including pre-existing overlaps");
            Check(sawKnockback && monster.Position.X > initialX, "knockback survives movement updates");
            Check(player.State == ActorState.Free && !player.AttackBox.Active, "attack end closes hitbox");
            Check(player.TryAttack(), "second attack starts");
            await Frames(4);
            Check(monster.Health == monster.MaxHealth - 24, "next swing can damage same target");
            player.ReceiveHit(new HitResult(1, new(-60, 0), 0.3f));
            Check(!player.AttackBox.Active && player.State == ActorState.Hurt, "hurt interrupts attack immediately");
            await Frames(30);
            Check(player.State == ActorState.Free && !player.AttackBox.Active, "interrupted timeline cannot reopen attack");
            monster.Team = player.Team;
            Check(!CombatResolver.Resolve(player, monster, player.NormalAttack), "friendly fire rejected");
            monster.Team = 1;
            monster.Position = player.Position + new Vector2(38, 0);
            monster.Face(-1);
            await Frames(3);
            float health = player.Health;
            Check(monster.TryAttack(), "monster attack starts");
            await Frames(30);
            Check(player.Health == health - 8, "monster timeline damages player");
            await Frames(30);
            player.InputEnabled = true;
            Input.ActionPress("jump");
            await Frames(2);
            Input.ActionRelease("jump");
            Check(player.Velocity.Y < 0, "player jumps from floor");
            await Frames(65);
            Check(player.IsOnFloor(), "player lands");
            player.InputEnabled = false;
            // Mirror the same attack to verify visuals and attack geometry face together.
            monster.Position = player.Position - new Vector2(70, 0);
            player.Face(-1);
            await Frames(3);
            health = monster.Health;
            player.TryAttack();
            await Frames(28);
            Check(monster.Health == health - 12, "left-facing attack");
            monster.ReceiveHit(new HitResult(1000, Vector2.Zero, 0));
            Check(monster.IsDead && !monster.TryAttack(), "death blocks attacks");
            Check(!CombatResolver.Resolve(player, monster, player.NormalAttack), "dead targets ignored");
            await Frames(10);
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://Tests/combat-preview.png");
            }
            GD.Print("COMBAT_SMOKE_TEST: PASS");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"COMBAT_SMOKE_TEST: FAIL {error}");
            GetTree().Quit(1);
        }
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        GD.Print($"PASS: {description}");
    }
}
