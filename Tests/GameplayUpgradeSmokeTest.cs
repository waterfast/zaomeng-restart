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
using Zaomeng.Save.Serialization;
using Zaomeng.Skills;
using Zaomeng.UI;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

public partial class GameplayUpgradeSmokeTest : Node
{
	public override void _Ready() => GetTree().Root.CallDeferred(Node.MethodName.AddChild, new GameplayUpgradeRunner());
}

/// <summary>独立进程中注入临时会话，槽位为0，所有保存操作都不会写入玩家存档。</summary>
public partial class GameplayUpgradeRunner : Node
{
	private SaveCharacter _character = null!;
	private InventoryService _inventory = null!;
	private ItemCatalog _catalog = null!;
	private readonly bool _capture = OS.GetCmdlineUserArgs().Contains("--capture-gameplay");

	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			PrepareTemporarySession();
			GameplayLevel level = await EnterForest();
			Player player = level.GetNode<Player>("Player");
			ForestEncounter encounter = level.GetChildren().OfType<ForestEncounter>().Single();
			encounter.SetPhysicsProcess(false);
			player.InputEnabled = false;
			Check(Enumerable.Range(0, 5).All(slot => player.GetEquippedSkill(slot) is null), "unlearned skills leave empty slots");
			await Capture("forest");
			await CheckSkillPanel(level, player);
			await CheckForestPresentation(level, player);
			await CheckMovement(level, player);
			await CheckSkills(level, player);
			await CheckSettings(level);
			level = await EnterForest();
			player = level.GetNode<Player>("Player");
			encounter = level.GetChildren().OfType<ForestEncounter>().Single();
			player.InputEnabled = false;
			player.SetPhysicsProcess(false);
			Engine.TimeScale = 20;
			var lethal = new HitDefinition { FlatDamage = 10000, DamageType = DamageType.True, CanCrit = false };
			int defeated = 0;
			bool bossSeen = false;
			bool advanceSeen = false;
			for (int frame = 0; frame < 1800 && GetTree().CurrentScene == level && !encounter.IsCleared; frame++)
			{
				int wave = encounter.Wave;
				if (!advanceSeen && encounter.NeedsAdvance)
				{
					advanceSeen = true;
					encounter.SetPhysicsProcess(false);
					Engine.TimeScale = 1;
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					var prompt = level.GetNode<AnimatedSprite2D>("OldHud/roleLayer/Gogo");
					Check(prompt.Visible && prompt.IsPlaying(), "wave clear plays original advance animation");
					await Capture("forest-advance");
					Engine.TimeScale = 20;
					encounter.SetPhysicsProcess(true);
				}
				player.Position = new(wave switch { 1 => 380, 2 => 1950, 3 => 2850, _ => 3800 }, 502);
				foreach (Monster monster in level.GetNode("Enemies").GetChildren().OfType<Monster>())
				{
					if (!monster.Visible || monster.IsDead) continue;
					monster.AiEnabled = false;
					if (monster.IsBoss)
					{
						Check(wave == 5 && defeated == 43 && monster.MaxHealth == 300, "boss follows all four original waves");
						bossSeen = true;
						Engine.TimeScale = 1;
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						var bar = level.GetNode<TextureProgressBar>("OldHud/roleLayer/BossBlood");
						Check(bar.Visible && bar.Value == 300, "original boss bar binds full health");
						monster.ReceiveHit(new HitResult(30, Vector2.Zero, 0, DamageType.True));
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						Check(bar.Value == 270 && bar.GetNode<Label>("BloodValue").Text == "270/300", "boss bar follows damage");
						await Capture("boss");
						Engine.TimeScale = 20;
					}
					if (CombatResolver.Resolve(player, monster, lethal)) defeated++;
				}
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			}
			Engine.TimeScale = 1;
			// 前面的自动战斗关闭了人工输入，出口交互需恢复正常玩家控制。
			player.InputEnabled = true;
			for (int frame = 0; frame < 600 && !level.HasNode("LevelExit"); frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			var exit = level.GetNode<LevelExit>("LevelExit");
			Check(encounter.Boss is { Visible: false }, "boss corpse is recycled before exit becomes available");
			Check(encounter.IsCleared && !GetTree().Paused && player.InputEnabled && !level.HasNode("Settlement") && GameSession.Data!.UnlockedLevel == 1,
				"boss defeat shows exit and keeps player free without committing victory");
			player.ResetForSpawn(new(3500, 502));
			SendExitKey(true);
			Check(!exit.CanActivate && !level.HasNode("Settlement") && player.InputEnabled, "W outside exit cannot settle");
			SendExitKey(false);
			player.ResetForSpawn(new(exit.Position.X, 502));
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Check(exit.CanActivate && !level.HasNode("Settlement"), $"approaching exit alone cannot settle (player={player.GlobalPosition}, exit={exit.GlobalPosition}, input={player.InputEnabled}, dead={player.IsDead})");
			SendExitKey(true, echo: true);
			Check(player.InputEnabled && !level.HasNode("Settlement"), "held W repeat cannot activate exit");
			await Capture("victory-exit");
			SendExitKey(true);
			Check(!player.InputEnabled, "exit consumes W before character skill input");
			SendExitKey(false);
			for (int frame = 0; frame < 60 && !level.HasNode("Settlement"); frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			var settlement = level.GetNode<LevelSettlement>("Settlement");
			Check(settlement.Result.Victory && GetTree().Paused && GameSession.Data!.UnlockedLevel == 2, "victory opens settlement and commits unlock without leaving level");
			Node2D resultView = settlement.GetChildren().OfType<Node2D>().Single();
			var resultAnimation = resultView.GetNode<AnimationPlayer>("GradesSHow");
			for (int frame = 0; frame < 600 && resultAnimation.IsPlaying(); frame++)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(!resultView.GetNode<BaseButton>("return_map").Disabled && resultView.HasNode("Rating"), "victory animation enables navigation and displays rating while world is paused");
			await Capture("victory-settlement");
			resultView.GetNode<BaseButton>("MoreInformaition").EmitSignal(BaseButton.SignalName.Pressed);
			await Capture("victory-details");
			settlement.GetNode<Node2D>("Details").GetNode<BaseButton>("Glose").EmitSignal(BaseButton.SignalName.Pressed);
			resultView.GetNode<BaseButton>("return_map").EmitSignal(BaseButton.SignalName.Pressed);
			await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
			Check(bossSeen && advanceSeen && defeated == 44 && GetTree().CurrentScene.SceneFilePath == GameSession.FirstMap, "boss defeat completes level and returns to map");
			Check(GameSession.Data!.UnlockedLevel == 2 && _inventory.GetItemCount("dshl") == 1 && _inventory.GetItemCount("dslj") >= 1, "completion unlocks water cave and awards equipment");
			Check(GetTree().CurrentScene.GetNode<TextureButton>("level_2").Disabled == false, "map reflects unlocked level");
			await Capture("map");
			if (_capture)
			{
				foreach (string name in new[] { "Skill_learn", "bc_game", "turn_mainmenu" })
				{
					var button = GetTree().CurrentScene.GetNode<TextureButton>(name);
					button.EmitSignal(Control.SignalName.MouseEntered);
					await Capture($"map-hover-{name}");
					button.EmitSignal(BaseButton.SignalName.ButtonDown);
					await Capture($"map-pressed-{name}");
					button.EmitSignal(BaseButton.SignalName.ButtonUp);
					button.EmitSignal(Control.SignalName.MouseExited);
				}
			}
			GD.Print("GAMEPLAY UPGRADE SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			Engine.TimeScale = 1;
			GetTree().Paused = false;
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private async Task CheckForestPresentation(GameplayLevel level, Player player)
	{
		player.Position = new(550, 502);
		var monsters = new System.Collections.Generic.List<Monster>();
		foreach (string path in new[] { "Monster", "Monster2", "ForestBoss" })
		{
			var monster = GD.Load<PackedScene>($"res://Scenes/Actors/{path}.tscn").Instantiate<Monster>();
			monster.AiEnabled = false;
			level.GetNode("Enemies").AddChild(monster);
			monster.ResetForSpawn(new(680 + monsters.Count * 110, 502));
			monsters.Add(monster);
		}
		await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
		foreach (Monster monster in monsters)
		{
			var sprite = monster.GetNode<AnimatedSprite2D>("Facing/Visual/Body");
			Texture2D texture = sprite.SpriteFrames.GetFrameTexture(sprite.Animation, sprite.Frame);
			Rect2I pixels = texture.GetImage().GetUsedRect();
			float feet = sprite.ToGlobal(sprite.Offset + new Vector2(0, pixels.End.Y - texture.GetHeight() / 2f)).Y;
			Check(monster.IsOnFloor() && Mathf.Abs(feet - 504) < 4, $"{monster.Name} visible feet align with collision floor ({feet})");
		}
		await Capture("forest-monster-feet");
		var hit = new HitDefinition { FlatDamage = 1, AttackMultiplier = 0, DamageType = DamageType.True, CanCrit = false, Knockback = Vector2.Zero };
		CombatResolver.Resolve(player, monsters[1], hit);
		CombatResolver.Resolve(player, monsters[1], hit);
		await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
		Check(level.GetNode("OldHud").GetChildren().OfType<ComboHud>().Single().Count == 2, "actual hits show original combo counter");
		await Capture("forest-combo");
		await ToSignal(GetTree().CreateTimer(1.7), SceneTreeTimer.SignalName.Timeout);
		Check(level.GetNode("OldHud").GetChildren().OfType<ComboHud>().Single().Count == 0, "combo resets after original timeout");
		foreach (Monster monster in monsters) monster.QueueFree();
		player.Position = new(1640, 437);
		player.Velocity = Vector2.Zero;
		for (int frame = 0; frame < 30; frame++)
		{
			player.Motor.Step(player, 200, player.Gravity, player.KnockbackFriction, 1f / 60);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		}
		Camera2D camera = level.GetNode<Camera2D>("Camera2D");
		camera.ResetSmoothing();
		camera.ForceUpdateScroll();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		float screenRight = camera.GetScreenCenterPosition().X + GetViewport().GetVisibleRect().Size.X / 2;
		Check(player.Position.X < 1645 && Mathf.Abs(screenRight - 1701) < 2, $"wave wall stops player at visible right edge ({screenRight})");
		await Capture("forest-wave-wall");
	}

	private void PrepareTemporarySession()
	{
		_catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		_catalog.Validate();
		_character = new SaveCharacter { Id = "role_1", Name = "孙悟空" };
		CharacterProgression.SyncBaseStats(_character);
		_inventory = new InventoryService(70, _catalog);
		var data = InventorySaveMapper.Capture(_inventory);
		data.Characters.Add(_character);
		data.Wallet.Souls = 50000;
		foreach (var (property, value) in new (string, object)[] { ("Slot", 0), ("Data", data), ("Inventory", _inventory) })
			typeof(GameSession).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
	}

	private async Task<GameplayLevel> EnterForest()
	{
		GetTree().ChangeSceneToFile(GameSession.FirstLevel);
		await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
		return (GameplayLevel)GetTree().CurrentScene;
	}

	private async Task CheckSkillPanel(GameplayLevel level, Player player)
	{
		var menu = level.GetNode<MenuManager>("MenuManager");
		menu.ToggleMenu("skills_menu");
		Node2D panel = level.GetNode("HUD").GetChildren().OfType<Node2D>().Single(node => node.SceneFilePath.EndsWith("Learn_skill.tscn"));
		string row = "bg/lh_pic/front_bg/zd_skill/ScrollContainer/HBoxContainer";
		long balance = GameSession.Data!.Wallet.Souls;
		panel.GetNode<Button>($"{row}/sk_lv/Skill_2").EmitSignal(BaseButton.SignalName.Pressed);
		Check(_character.SkillLevels["hytj"] == 1 && GameSession.Data!.Wallet.Souls == balance - 400, "learning button spends original soul price");
		panel.GetNode<Button>($"{row}/sk_pi/ski_2").EmitSignal(BaseButton.SignalName.Pressed);
		await Capture("skill-keys");
		var keyButton = panel.GetNode<Button>("SkillKeySet/BG/U");
		keyButton.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
		Input.ParseInputEvent(new InputEventKey { Pressed = true, Keycode = Key.X, PhysicalKeycode = Key.X });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Input.ParseInputEvent(new InputEventKey { Pressed = false, Keycode = Key.X, PhysicalKeycode = Key.X });
		Check(_character.SkillKeyCodes[1] == (long)Key.X, "key dialog persists custom physical key");
		keyButton.EmitSignal(BaseButton.SignalName.Pressed);
		Check(player.EquippedSkill2?.Id == "hytj" && _character.EquippedSkillIds[1] == "hytj", "key dialog equips learned skill");
		await Capture("skills");
		panel.GetNode<Button>("bg/bd_skill").EmitSignal(BaseButton.SignalName.Pressed);
		await Capture("passive-skills");
		panel.GetNode<Button>("bg/close").EmitSignal(BaseButton.SignalName.Pressed);
		Check(!GetTree().Paused, "closing skill panel resumes game");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		foreach (string key in new[] { "Y", "U", "I", "O", "L" })
		{
			var slot = level.GetNode<TextureRect>($"OldHud/roleLayer/role_menu/SkillBox/{key}");
			Check(slot.Texture is not null && slot.GetNode<TextureRect>("Icon").Position == new Vector2(4, 4), $"HUD slot {key} retains an independent frame");
		}
		Check(level.GetNode<TextureRect>("OldHud/roleLayer/role_menu/SkillBox/U/Icon").Texture is not null &&
			level.GetNode<Label>("OldHud/roleLayer/role_menu/SkillBox/U/Y").Text == "X", "lower-left skill slot shows icon and custom key");
		var serializer = new JsonSaveSerializer();
		var restored = serializer.Deserialize(serializer.Serialize(GameSession.Data!));
		Check(restored.Characters[0].SkillKeyCodes[1] == (long)Key.X && restored.Characters[0].EquippedSkillIds[1] == "hytj", "learning and bindings round-trip through save serialization");
		var service = new SkillLearningService(_character, GameSession.Data!.Wallet);
		long after = service.Souls;
		Check(service.TryLearn("hytj", out _) && service.Level("hytj") == 2 && service.Souls == after - 900,
			"learning a known active skill upgrades it with the next-level cost");
		service.Apply(player);
		Check(!service.TryEquip(0, "slz", out _) && !service.TrySetKey(0, Key.J, out _), "unlearned skill and reserved key rejected");
	}

	private async Task CheckMovement(GameplayLevel level, Player player)
	{
		player.SetPhysicsProcess(false);
		foreach (CollisionShape2D gate in level.GetNode("WaveGates").GetChildren()) gate.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		player.ResetForSpawn(new(900, 500));
		for (int i = 0; i < 300; i++)
		{
			player.Motor.Step(player, 240, player.Gravity, player.KnockbackFriction, 1f / 30);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		}
		Check(player.Position.X > 1950 && player.Position.Y < 520, $"player crosses both forest slopes without snagging or sinking: {player.Position}");
		var wall = new StaticBody2D { Position = new(2200, 250), CollisionLayer = 1 };
		wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new(20, 500) } });
		level.AddChild(wall);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		player.Motor.ApplyKnockback(player, new(1500, 0));
		for (int i = 0; i < 20; i++)
		{
			player.Motor.Step(player, 1000, player.Gravity, player.KnockbackFriction, 1f / 30);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		}
		Check(player.Position.X <= 2172, "dash plus knockback cannot penetrate wall");
		player.Motor.ApplyKnockback(player, Vector2.Zero);
		for (int i = 0; i < 40; i++)
		{
			player.Motor.Step(player, -240, player.Gravity, player.KnockbackFriction, 1f / 30);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		}
		Check(player.Position.X < 2050, "player can move away after colliding with wall");
		wall.QueueFree();
		player.SetPhysicsProcess(true);
	}

	private async Task CheckSkills(GameplayLevel level, Player player)
	{
		var service = new SkillLearningService(_character, GameSession.Data!.Wallet);
		var target = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
		target.AiEnabled = false;
		target.MaxHealth = 10000;
		level.AddChild(target);
		target.Position = new(2070, 502);
		target.SetPhysicsProcess(false);
		foreach (SkillEntry skill in service.Catalog.Skills.Where(skill => !skill.Passive))
		{
			if (service.Level(skill.Id) == 0) Check(service.TryLearn(skill.Id, out _), $"learn {skill.Id}");
			player.ResetForSpawn(new(1980, 502));
			player.Face(1);
			player.RestoreMana(player.MaxMana);
			Check(player.TryUseSkill(skill.Action), $"cast original skill {skill.Id}");
			await ToSignal(GetTree().CreateTimer(1.8), SceneTreeTimer.SignalName.Timeout);
			Check(player.State == ActorState.Free && player.Gravity == 600, $"skill {skill.Id} returns to free movement");
		}
		Check(target.Health < target.MaxHealth, "migrated skill effects deal damage");
		Check(service.TryLearn("sx", out _), "learn passive bloodthirst");
		service.Apply(player);
		Check(player.LifeSteal >= 0.03f && player.CriticalRating >= 4, "passive adds critical rating and lifesteal");
		player.ReceiveHit(new HitResult(25, Vector2.Zero, 0));
		player.TrySpendMana(player.Mana);
		player.GainExperience(player.ExperienceToNextLevel);
		Check(player.Health == player.MaxHealth && player.Mana == player.MaxMana, "level-up fills health and mana using new limits");
		target.QueueFree();
		var boss = GD.Load<PackedScene>("res://Scenes/Actors/ForestBoss.tscn").Instantiate<Monster>();
		boss.AiEnabled = false;
		level.AddChild(boss);
		boss.ResetForSpawn(new(2100, 502));
		boss.Face(-1);
		player.ResetForSpawn(new(1980, 502));
		player.SetPhysicsProcess(false);
		float health = player.Health;
		Check(boss.TryAttack(), "boss starts original gorilla attack");
		await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
		Check(player.Health < health, "gorilla attack hits at its configured AI range");
		boss.QueueFree();
	}

	private async Task CheckSettings(GameplayLevel level)
	{
		MenuManager menu = level.GetNode<MenuManager>("MenuManager");
		menu.ToggleMenu("pause_menu");
		Control settings = level.GetNode("HUD").GetChildren().OfType<Control>().Single(node => node.SceneFilePath.EndsWith("SetMenu.tscn"));
		Check(GetTree().Paused && settings.Visible, "settings pause the level");
		await Capture("settings");
		settings.GetNode<Button>("bg/box/continue_game").EmitSignal(BaseButton.SignalName.Pressed);
		Check(!GetTree().Paused, "continue resumes level");
		menu.ToggleMenu("pause_menu");
		settings.GetNode<Button>("bg/box/continue_game2").EmitSignal(BaseButton.SignalName.Pressed);
		await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
		Check(!GetTree().Paused && GetTree().CurrentScene.SceneFilePath == GameSession.FirstMap, "return-to-map releases pause");
		var map = GetTree().CurrentScene;
		map.GetNode<TextureButton>("Skill_learn").EmitSignal(BaseButton.SignalName.Pressed);
		Check(GetTree().Paused, "main map opens shared skill panel");
		map.GetChildren().OfType<MenuManager>().Single().CloseMenu();
		map.GetNode<TextureButton>("turn_mainmenu").EmitSignal(BaseButton.SignalName.Pressed);
		await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
		Check(!GetTree().Paused && GetTree().CurrentScene.SceneFilePath.EndsWith("MainMenu.tscn"), "main map returns to main menu");
	}

	private async Task Capture(string name)
	{
		if (!_capture) return;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string directory = ProjectSettings.GlobalizePath("res://.godot/gameplay-checks");
		System.IO.Directory.CreateDirectory(directory);
		GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
	}

	private static void SendExitKey(bool pressed, bool echo = false)
	{
		Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Pressed = pressed, Echo = echo });
		// 模拟输入默认缓冲到下一帧，先派发再传送玩家，避免测试改变按键发生地点。
		Input.FlushBufferedEvents();
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
		GD.Print($"PASS: {message}");
	}
}
