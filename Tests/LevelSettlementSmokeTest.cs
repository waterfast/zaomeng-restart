using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Save;
using Zaomeng.UI;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

public partial class LevelSettlementSmokeTest : Node
{
	public override void _Ready() => GetTree().Root.CallDeferred(Node.MethodName.AddChild, new SettlementTestRunner());
}
public partial class SettlementTestRunner : Node
{
	private readonly bool _capture = OS.GetCmdlineUserArgs().Contains("--capture-settlement");
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			foreach (string role in new[] { "role_1", "role_2" })
			{
				Prepare(role);
				GameplayLevel level = await Enter();
				Player player = level.GetNode<Player>("Player");
				ForestEncounter forest = level.GetChildren().OfType<ForestEncounter>().Single();
				forest.SetPhysicsProcess(false);
				if (role == "role_1")
				{
					var preview = new LevelSettlement();
					level.AddChild(preview);
					using var statistics = new LevelRunStatistics(player);
					preview.ShowResult(statistics.Finish(true, "花果山", 12));
					GetTree().Paused = true;
					await Delay(2);
					Node2D resultView = preview.GetChildren().OfType<Node2D>().Single();
					var animation = resultView.GetNode<AnimationPlayer>("GradesSHow");
					Check(!resultView.GetNode<BaseButton>("return_map").Disabled && resultView.HasNode("Rating"), $"victory animation completes while paused ({animation.CurrentAnimationPosition})");
					await Capture("victory-layout");
					preview.QueueFree();
					GetTree().Paused = false;
				}
				if (role == "role_1")
				{
					typeof(ForestEncounter).GetProperty("Wave")!.SetValue(forest, 5);
					foreach (CollisionShape2D gate in level.GetNode("WaveGates").GetChildren()) gate.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
					var finalGate = (CollisionShape2D)level.GetNode("WaveGates").GetChild(3);
					finalGate.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
					player.ResetForSpawn(new(3920, 502));
					player.SetPhysicsProcess(false);
					await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
					for (int frame = 0; frame < 45; frame++)
					{
						player.Motor.Step(player, 600, player.Gravity, player.KnockbackFriction, 1f / 60);
						await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
					}
					Check(player.Position.X is > 3960 and < 4000 && forest.CameraRight == 4040, "final wall stops player on flat ground before slope and empty area");
					level.GetNode<Camera2D>("Camera2D").ResetSmoothing();
					await Capture("boss-boundary");
					player.SetPhysicsProcess(true);
				}
				player.ResetForSpawn(new(500, 502));
				await Delay(0.2);
				level.GetNode<Camera2D>("Camera2D").ResetSmoothing();
				player.ReceiveHit(new HitResult(10000, Vector2.Zero, 0, DamageType.True));
				await Delay(0.12);
				Check(player.IsDead && player.GetNode<AnimatedSprite2D>("Facing/Visual/Death") is { Visible: true } death && death.IsPlaying() && !level.HasNode("Settlement"), $"{role} death effect runs before settlement");
				await Delay(0.2);
				await Capture(role + "-death");
				for (int frame = 0; frame < 420 && !level.HasNode("Settlement"); frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				var settlement = level.GetNode<LevelSettlement>("Settlement");
				Check(!player.GetNode<AnimatedSprite2D>("Facing/Visual/Death").IsPlaying(), "failure waits for the complete independent death sprite animation");
				Check(!settlement.Result.Victory && settlement.Result.Health == 0 && settlement.Result.HurtCount == 1 && GetTree().Paused && GameSession.Data!.UnlockedLevel == 1, "death opens failure result without unlocking next level");
				await Capture(role + "-defeat");
				Node2D view = settlement.GetChildren().OfType<Node2D>().Single();
				view.GetNode<BaseButton>("Details").EmitSignal(BaseButton.SignalName.Pressed);
				Check(settlement.GetNode<Label>("Details/HBoxContainer/VBoxContainer2/TotalHurtCount").Text.Contains("1"), "details show actual current-run damage counters");
				await Capture(role + "-details");
				settlement.GetNode<BaseButton>("Details/Glose").EmitSignal(BaseButton.SignalName.Pressed);
				view.GetNode<BaseButton>("ReChallenge").EmitSignal(BaseButton.SignalName.Pressed);
				await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
				level = (GameplayLevel)GetTree().CurrentScene;
				player = level.GetNode<Player>("Player");
				Check(!GetTree().Paused && !player.IsDead && player.Health == player.MaxHealth && player.Mana == player.MaxMana && !level.HasNode("Settlement"), "retry creates fresh run and restores resources");
				level.GetChildren().OfType<ForestEncounter>().Single().SetPhysicsProcess(false);
				player.ReceiveHit(new HitResult(10000, Vector2.Zero, 0, DamageType.True));
				for (int frame = 0; frame < 420 && !level.HasNode("Settlement"); frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				view = level.GetNode<LevelSettlement>("Settlement").GetChildren().OfType<Node2D>().Single();
				view.GetNode<BaseButton>("return_map").EmitSignal(BaseButton.SignalName.Pressed);
				await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
				Check(!GetTree().Paused && GetTree().CurrentScene.SceneFilePath == GameSession.FirstMap, "return from defeat releases pause and opens map");
			}
			GD.Print("LEVEL SETTLEMENT SMOKE TEST PASSED");
			GetTree().CurrentScene.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GetTree().Quit();
		}
		catch (Exception error) { GetTree().Paused = false; GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private void Prepare(string role)
	{
		var items = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		items.Validate();
		var character = new SaveCharacter { Id = role };
		CharacterProgression.SyncBaseStats(character);
		var inventory = new InventoryService(70, items);
		GameSaveData data = InventorySaveMapper.Capture(inventory);
		data.CurrentCharacterId = role;
		data.Characters.Add(character);
		foreach (var (property, value) in new (string, object)[] { ("Slot", 0), ("Data", data), ("Inventory", inventory) })
			typeof(GameSession).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
	}
	private async Task<GameplayLevel> Enter()
	{
		GetTree().ChangeSceneToFile(GameSession.FirstLevel);
		await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
		return (GameplayLevel)GetTree().CurrentScene;
	}
	private async Task Delay(double time) => await ToSignal(GetTree().CreateTimer(time, processInPhysics: true), SceneTreeTimer.SignalName.Timeout);
	private async Task Capture(string name)
	{
		if (!_capture) return;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string folder = ProjectSettings.GlobalizePath("res://.godot/settlement-checks");
		System.IO.Directory.CreateDirectory(folder);
		GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, name + ".png"));
	}
	private static void Check(bool passed, string message)
	{
		if (!passed) throw new InvalidOperationException(message);
		GD.Print("PASS: " + message);
	}
}
