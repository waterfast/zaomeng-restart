using Godot;
using System;

namespace Zaomeng;

public partial class TestArena : Node2D
{
    private Player _player = null!;
    private Monster _monster = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        _player = GetNode<Player>("Player");
        _monster = GetNode<Monster>("Monster");
        _status = GetNode<Label>("HUD/Panel/Status");
        if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--smoke-test"))
            CallDeferred(MethodName.StartSmokeTest);
    }

    private void StartSmokeTest() => AddChild(new CombatSmokeTest());

    public override void _Process(double delta)
    {
        string state = _player.State switch
        {
            ActorState.Attacking => "攻击",
            ActorState.Hurt => "受击",
            ActorState.Dead => "倒下",
            _ => "可行动"
        };
        _status.Text = $"悟空 {_player.Health:0}/{_player.MaxHealth:0}　小猴 {_monster.Health:0}/{_monster.MaxHealth:0}\n"
            + $"状态：{state}　敌人 AI：{(_monster.AiEnabled ? "开启" : "木桩")}"
            + (_player.IsDead || _monster.IsDead ? "　按 R 重置" : "");
        var camera = GetNode<Camera2D>("Camera2D");
        camera.Position = new(_player.Position.X, 280);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("restart")) GetTree().ReloadCurrentScene();
        if (input.IsActionPressed("toggle_ai")) _monster.AiEnabled = !_monster.AiEnabled;
        if (input.IsActionPressed("debug_shapes"))
            GetTree().DebugCollisionsHint = !GetTree().DebugCollisionsHint;
    }
}
