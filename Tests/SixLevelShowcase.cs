using System;
using Godot;
using Zaomeng.Level;
using Zaomeng.Monsters;

namespace Zaomeng;

/// <summary>原生渲染巡检：固定镜头查看六关地形、首领与地刺。</summary>
public partial class SixLevelShowcase : Node2D
{
	public override async void _Ready()
	{
		try
		{
			DirAccess.MakeDirRecursiveAbsolute("res://.godot/six-level-checks");
			for (int number = 4; number <= 9; number++)
			{
				var definition = GD.Load<LevelDefinition>($"res://Content/Levels/human_{number}.tres");
				var world = new Node2D(); AddChild(world);
				world.AddChild(definition.MapScene!.Instantiate());
				var camera = new Camera2D { Position = new(4300, 310) }; world.AddChild(camera);
				var bossDefinition = definition.WaveEncounter!.Waves[3].Monsters[^1];
				for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				var ray = PhysicsRayQueryParameters2D.Create(new(4400, -200), new(4400, 900), 1);
				var ground = world.GetWorld2D().DirectSpaceState.IntersectRay(ray);
				var boss = new MonsterPool(world).Spawn(bossDefinition, ground["position"].AsVector2());
				boss.AiEnabled = false; boss.SetPhysicsProcess(false);
				var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
				world.AddChild(player); player.Position = new(4150, boss.Position.Y); player.InputEnabled = false; player.SetPhysicsProcess(false);
				for (int frame = 0; frame < 4; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng($"res://.godot/six-level-checks/{number}-boss.png");
				camera.Position = new(1450, 310);
				for (int frame = 0; frame < 4; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng($"res://.godot/six-level-checks/{number}-terrain.png");
				world.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			GD.Print("SIX LEVEL SHOWCASE PASSED"); GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
}
