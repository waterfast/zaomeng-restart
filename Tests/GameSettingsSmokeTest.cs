using System;
using System.Threading.Tasks;
using Godot;
using Zaomeng;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Settings;

/// <summary>隔离设置文件验证菜单、持久化和地面自动拾取，不写玩家存档。</summary>
public partial class GameSettingsSmokeTest : Node
{
	private const string TestPath = "user://settings_smoke_test.cfg";
	public override async void _Ready()
	{
		try
		{
			DirAccess.RemoveAbsolute(TestPath);
			GameSettings.Load(TestPath);
			Check(!GameSettings.AutomaticPickup, "自动拾取默认关闭");
			var menu = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/MainMenu.tscn").Instantiate<Node2D>();
			AddChild(menu);
			var entry = menu.GetNode<Button>("ButtonList/GameSet");
			Check(!entry.Disabled, "主菜单游戏设置入口可用");
			entry.EmitSignal(BaseButton.SignalName.Pressed);
			Node2D panel = menu.GetNode<Node2D>("SettingsLayer/GameSet");
			const string togglePath = "Bg/BGColor/VBoxContainer2/AutomaticallyPickUpItems/YesOrNots";
			panel.GetNode<Button>(togglePath).EmitSignal(BaseButton.SignalName.Pressed);
			Check(GameSettings.AutomaticPickup && panel.GetNode<Button>(togglePath).Text == "开启中", "实际自动拾取按钮更新设置与显示");
			panel.GetNode<HSlider>("Bg/BGColor/VBoxContainer/GameMusicFB/HSlider").Value = -15;
			GameSettings.Load(TestPath);
			Check(GameSettings.AutomaticPickup && GameSettings.Volume(true) == -15
				&& AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Music")) == -15, "设置重新读取后保持开关和音量");
			if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--capture-settings"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				DirAccess.MakeDirRecursiveAbsolute("res://.godot/settings-checks");
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/settings-checks/main-settings.png");
			}
			panel.GetNode<Button>("Bg/Return").EmitSignal(BaseButton.SignalName.Pressed);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(!menu.HasNode("SettingsLayer"), "设置返回按钮关闭面板");
			entry.EmitSignal(BaseButton.SignalName.Pressed);
			panel = menu.GetNode<Node2D>("SettingsLayer/GameSet");
			Check(panel.GetNode<Button>(togglePath).Text == "开启中", "重开设置显示已保存的状态");
			menu.QueueFree();

			var world = new Node2D();
			AddChild(world);
			var floor = new StaticBody2D { Position = new(500, 310), CollisionLayer = 1 };
			floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new(1000, 20) } });
			world.AddChild(floor);
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			world.AddChild(player);
			player.InputEnabled = false;
			player.SetPhysicsProcess(false);
			player.Position = new(100, 300);
			GameSettings.SetEnabled(GameOption.PlayerBody, false);
			Check(player.GetNode<CanvasItem>("Facing/Visual/RoleBody").Modulate.A == 0 && player.CollisionLayer != 0,
				"隐藏角色身体只影响显示，保留碰撞");
			GameSettings.SetEnabled(GameOption.PlayerBody, true);
			Check(player.GetNode<CanvasItem>("Facing/Visual/RoleBody").Modulate.A == 1, "身体显示开关立即恢复显示");
			GameSettings.SetEnabled(GameOption.Music, false);
			Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "音乐开关控制共享音频总线");
			GameSettings.SetEnabled(GameOption.Music, true);
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			catalog.TryGetDefinition("ptxzg", out var item);
			var bag = new InventoryService(1, catalog);
			int collects = 0;
			ItemPickup Spawn()
			{
				var pickup = GD.Load<PackedScene>("res://Scenes/Effects/ItemPickup.tscn").Instantiate<ItemPickup>();
				pickup.Position = new(600, 276);
				pickup.Configure(item!, 1, player, drop =>
				{
					if (!bag.AddItem(drop.Item.Id, drop.Count)) return false;
					collects++;
					return true;
				});
				world.AddChild(pickup);
				return pickup;
			}
			Engine.TimeScale = 6;
			var first = Spawn();
			await Delay(2);
			Check(bag.GetItemCount(item!.Id) == 0 && first.IsOnFloor(), "自动拾取等待落地满3秒才入包");
			GetTree().Paused = true;
			await ToSignal(GetTree().CreateTimer(0.3, true, false, true), SceneTreeTimer.SignalName.Timeout);
			Check(bag.GetItemCount(item.Id) == 0, "暂停期间不自动拾取");
			GetTree().Paused = false;
			await Delay(2);
			Check(bag.GetItemCount(item.Id) == 1 && collects == 1, "远处掉落等待后只领取一次");
			GameSettings.SetEnabled(GameOption.AutomaticPickup, false);
			var second = Spawn();
			await Delay(4);
			Check(!second.Collected && bag.GetItemCount(item.Id) == 1, "关闭自动拾取后远处掉落保留");
			player.Position = second.Position;
			await Delay(0.4);
			Check(bag.GetItemCount(item.Id) == 2 && bag.Capacity > 1, "走近拾取仍可用且背包按需扩容");
			player.Position = new(100, 300);
			GameSettings.SetEnabled(GameOption.AutomaticPickup, true);
			var third = Spawn();
			player.ReceiveHit(new HitResult(100000, Vector2.Zero, 0, DamageType.True));
			await Delay(4);
			Check(player.IsDead && !third.Collected && bag.GetItemCount(item.Id) == 2, "死亡后不自动领取掉落");
			GD.Print("GameSettingsSmokeTest PASS");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError($"GameSettingsSmokeTest FAIL: {error}"); GetTree().Quit(1); }
		finally
		{
			Engine.TimeScale = 1;
			GetTree().Paused = false;
			DirAccess.RemoveAbsolute(TestPath);
			GameSettings.Load();
		}
	}
	private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, false), SceneTreeTimer.SignalName.Timeout);
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
