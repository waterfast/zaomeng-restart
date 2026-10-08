using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Character;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Save;
using Zaomeng.UI.MainMenu;

namespace Zaomeng;

public partial class HumanCampaignSmokeTest : Node
{
	public override void _Ready() => GetTree().Root.CallDeferred(Node.MethodName.AddChild, new HumanCampaignRunner());
}

/// <summary>槽位0；物理移动全程不传送，战斗清场用于隔离地形与流程验证。</summary>
public partial class HumanCampaignRunner : Node
{
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			var inventory = new InventoryService(70, catalog);
			var character = new Character.Character { Id = "role_1", Name = "孙悟空" };
			CharacterProgression.SyncBaseStats(character);
			var save = InventorySaveMapper.Capture(inventory);
			save.Characters.Add(character);
			foreach (var (property, value) in new (string, object)[] { ("Slot", 0), ("Data", save), ("Inventory", inventory) })
				typeof(GameSession).GetProperty(property)!.SetValue(null, value);
			var map = GD.Load<WorldMapDefinition>("res://Content/Maps/Human.tres");
			var campaign = map.LevelEntrances.Where(e => e.Levels[0].AdvancesCampaign).ToArray();
			Check(campaign.Length == 10, "人间十个主线入口已登记");
			bool realmOnly = OS.GetCmdlineUserArgs().Contains("--realm-only");
			if (realmOnly)
			{
				var world = GD.Load<PackedScene>(map.ScenePath).Instantiate<WorldMap>();
				AddChild(world);
				var portal = world.GetNode<TextureButton>("level_lhhj");
				Check(!portal.Disabled, "幻境传送门已启用");
				portal.EmitSignal(BaseButton.SignalName.Pressed);
				Check(world.GetChildren().OfType<CanvasLayer>().SelectMany(layer => layer.GetChildren())
					.OfType<LevelPreviewPanel>().Any(), "幻境传送门能打开挑战面板");
				world.QueueFree();
				await Frames(2);
			}
			var entrances = realmOnly ? map.LevelEntrances.Where(e => !e.Levels[0].AdvancesCampaign).ToArray() : campaign;
			foreach (var entrance in entrances)
			{
				var definition = entrance.Levels[0];
				if (OS.GetCmdlineUserArgs().Contains("--map-three-only") && definition.ProgressLevel != 3) continue;
				if (OS.GetCmdlineUserArgs().Contains("--six-only") && definition.ProgressLevel is < 4 or > 9) continue;
				int initialProgress = save.UnlockedLevel;
				if (OS.GetCmdlineUserArgs().Contains("--early-only") && definition.ProgressLevel > 3) break;
				definition.Validate(catalog);
				GameSession.SelectLevel(definition, 20, definition.Difficulties[0]);
				var level = GD.Load<PackedScene>(definition.LevelScenePath).Instantiate<GameplayLevel>();
				AddChild(level);
				var player = level.GetNode<Player>("Player");
				await Frames(10);
				// 地形巡检隔离陷阱伤害；地刺周期和命中由专项测试覆盖。
				foreach (var trap in level.GetNode("Map").GetChildren().OfType<Zaomeng.Level.Traps.GroundSpikeTrap>()) trap.SetPhysicsProcess(false);
				if (realmOnly)
				{
					var terrain = level.GetNode<TileMapLayer>("Map/Ground");
					var point = new PhysicsPointQueryParameters2D { CollisionMask = 1, Position = new Vector2(352, 544) };
					Check(terrain.GetWorld2D().DirectSpaceState.IntersectPoint(point).Count > 0, "幻境岩石格有实体碰撞");
					point.Position = new Vector2(800, 544);
					Check(terrain.GetWorld2D().DirectSpaceState.IntersectPoint(point).Count == 0, "幻境水格不产生实体碰撞");
					point.Position = new Vector2(-32, 320);
					Check(terrain.GetWorld2D().DirectSpaceState.IntersectPoint(point).Count > 0, "幻境左侧岩石格封住地图边界");
				}
				await Capture($"{definition.ProgressLevel}-start");
				var encounter = level.GetChildren().OfType<WaveEncounter>().FirstOrDefault();
				int defeated = 0;
				if (definition.ProgressLevel <= 3)
				{
					Check(encounter is not null && !definition.MapOnly, "前三关都有战斗遭遇");
					encounter!.SetPhysicsProcess(false);
					foreach (var monster in level.GetNode("Enemies").GetChildren().OfType<Monster>()) monster.AiEnabled = false;
					float gateX = definition.WaveEncounter!.Waves[0].GateX;
					Input.ActionPress("move_right");
					for (int i = 0; i < 600 && player.Position.X < gateX - 40; i++)
					{
						if (i % 20 == 0 && Mathf.Abs(player.Velocity.X) < 0.2f && player.IsOnFloor()) player.TryJump();
						await Frames(1);
					}
					await Frames(40);
					Check(player.Position.X < gateX && player.Position.X > gateX - 40, $"{definition.DisplayName} 首波空气墙实际阻挡玩家");
					encounter.SetPhysicsProcess(true);
				}
				Input.ActionPress("move_right");
				float lastX = player.Position.X;
				int stalled = 0;
				int platformStep = 0;
				LevelExit? exit = null;
				for (int frame = 0; frame < 4500; frame++)
				{
					// 按真实战斗接口清场，不跳过波次状态和门禁。
					foreach (var monster in level.GetNode("Enemies").GetChildren().OfType<Monster>())
					{
						monster.AiEnabled = false;
						// 移动与出口测试隔离首领机制，机制由专项实战测试覆盖。
						foreach (var mechanic in monster.GetChildren().OfType<Zaomeng.Monsters.MonsterMechanic>()) mechanic.SetPhysicsProcess(false);
						monster.Buffs.RemoveSource("mechanic:shield");
						if (monster.Visible && !monster.IsDead)
						{
							if (monster.IsBoss) await Capture($"{definition.ProgressLevel}-boss");
							if (CombatResolver.Resolve(player, monster, new HitDefinition { FlatDamage = 100000, AttackMultiplier = 0, CanCrit = false, DamageType = DamageType.True })) defeated++;
						}
					}
					if (Mathf.Abs(player.Position.X - lastX) < 0.2f) stalled++; else stalled = 0;
					lastX = player.Position.X;
					if (frame == 600) await Capture($"{definition.ProgressLevel}-route");
					exit = level.GetChildren().OfType<LevelExit>().FirstOrDefault();
					if (exit is not null)
					{
						Input.ActionRelease("move_right");
						Input.ActionRelease("move_left");
						if (Mathf.Abs(player.Position.X - exit.Position.X) > 10)
							Input.ActionPress(player.Position.X > exit.Position.X ? "move_left" : "move_right");
					}
					else
					{
						if (definition.ProgressLevel != 3 || !FollowPeachPlatform(player, ref platformStep))
						{
							if (stalled > 15 && player.IsOnFloor()) player.TryJump();
							if (stalled > 40 && !player.IsOnFloor() && player.Velocity.Y >= 0) player.TryJump();
						}
					}
					if (exit?.CanActivate == true && (definition.ProgressLevel > 3 ||
						Mathf.Abs(player.Position.X - exit.Position.X) <= 10 && player.IsOnFloor())) break;
					if (frame % 600 == 599) GD.Print($"MOVE {definition.Id}: x={player.Position.X:0.0}, y={player.Position.Y:0.0}, stalled={stalled}");
					Check(player.Position.Y < 1000 && !player.IsDead, $"{definition.Id} 不掉出地图");
					await Frames(1);
				}
				Input.ActionRelease("move_right");
				Input.ActionRelease("move_left");
				Check(exit?.CanActivate == true, $"{definition.DisplayName} 连续移动到出口 (x={player.Position.X:0.0}, y={player.Position.Y:0.0})");
				if (definition.ProgressLevel <= 3)
				{
					Check(encounter!.IsCleared && defeated == definition.WaveEncounter!.Waves.Sum(w => w.Monsters.Count), "全部怪物清场才开放出口");
					Check(Mathf.Abs(player.Position.Y - exit!.Position.Y) < 3, "光圈脚点与站立地面一致");
					await Capture($"{definition.ProgressLevel}-exit");
					float gateX = definition.WaveEncounter!.Waves.Where(w => w.GateX > 0).Last().GateX;
					Check(gateX - exit.Position.X >= 120, "出口右侧保留拾取和移动空间");
					Input.ActionPress("move_right");
					await Frames(120);
					Check(player.Position.X < gateX && player.Position.X > gateX - 40, "清场后最终空气墙仍然阻挡玩家");
					Input.ActionRelease("move_right");
					Input.ActionPress("move_left");
					for (int i = 0; i < 120 && !exit.CanActivate; i++) await Frames(1);
					Input.ActionRelease("move_left");
					Check(exit.CanActivate, "玩家可以从最终墙走回光圈");
				}
				exit!._Input(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
				Check(save.UnlockedLevel == (definition.AdvancesCampaign ? definition.ProgressLevel + 1 : initialProgress),
					$"{definition.DisplayName} W结算遵循主线/副本进度规则");
				GD.Print($"PASS WALK {definition.ProgressLevel}: {definition.DisplayName}");
				GetTree().Paused = false;
				level.QueueFree();
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			GD.Print("HUMAN CAMPAIGN SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			Input.ActionRelease("move_right");
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}
	// 原高柱需要先借悬空平台；巡检只使用真实移动和二段跳，不传送角色。
	private static bool FollowPeachPlatform(Player player, ref int step)
	{
		if (step == 0)
		{
			if (player.Position.X < 3000) return false;
			player.TryJump();
			step = 1;
		}
		if (step == 1)
		{
			if (player.Position.X >= 3158) Input.ActionRelease("move_right");
			if (!player.IsOnFloor() && player.Velocity.Y >= 0) player.TryJump();
			if (player.IsOnFloor() && player.Position.Y < 260)
			{
				Input.ActionPress("move_right");
				step = 2;
			}
			return true;
		}
		if (step == 2 && player.Position.X >= 3200)
		{
			player.TryJump();
			step = 3;
		}
		if (step == 3)
		{
			if (!player.IsOnFloor() && player.Velocity.Y >= 0) player.TryJump();
			if (player.Position.X > 3600) step = 4;
		}
		return step < 4;
	}

	private async Task Frames(int count)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	}
	private async Task Capture(string name)
	{
		if (!OS.GetCmdlineUserArgs().Contains("--capture-human")) return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		DirAccess.MakeDirRecursiveAbsolute("res://.godot/human-checks");
		GetViewport().GetTexture().GetImage().SavePng($"res://.godot/human-checks/{name}.png");
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
