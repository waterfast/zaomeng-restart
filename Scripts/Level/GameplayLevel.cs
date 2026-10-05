using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Equipment;
using Zaomeng.Events;
using Zaomeng.UI;
using Zaomeng.UI.Inventory;
using Zaomeng.Skills;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Level;

/// <summary>前三关共享的 C# 运行时；地图画面、怪物种类由场景配置。</summary>
public partial class GameplayLevel : Node2D
{
	[Export(PropertyHint.Range, "1,3,1")] public int LevelNumber { get; set; } = 1;
	[Export] public ItemCatalog ItemCatalog { get; set; } = null!;
	[Export] public GameplayEvents Events { get; set; } = null!;
	[Export] public LevelDefinition? DefaultDefinition { get; set; }
	[Export] public float SpawnInterval { get; set; } = 2.1f;
	[Export] public int MaximumMonsters { get; set; } = 5;

	private Player _player = null!;
	private Camera2D _camera = null!;
	private MonsterPool _pool = null!;
	private readonly Dictionary<Monster, (int Kind, float CorpseTime)> _active = new();
	private InventoryService _inventory = null!;
	private InventoryViewAdapter _inventoryAdapter = null!;
	private LegacyBackpackView _backpackView = null!;
	private SaveCharacter _character = null!;
	private GameplayEquipmentBinding _equipmentBinding = null!;
	private Label _status = null!;
	private readonly CapsuleShape2D _spawnClearance = new() { Radius = 18, Height = 60 };
	private float _spawnClock;
	private bool _transitioning;
	private ForestEncounter? _forest;
	private BossHud? _bossHud;
	private bool _ending;
	private bool _exitPending;
	private float _exitDelay;
	private LevelExit? _exit;
	private float _endDelay;
	private LevelResult? _result;
	private LevelRunStatistics _statistics = null!;
	private ComboHud _combo = null!;
	private AnimatedSprite2D _advancePrompt = null!;
	private readonly HashSet<Monster> _lootAwarded = new();
	private LevelDefinition _definition = null!;
	private float _selectedSpawnSpeed = 1;

	public override void _EnterTree()
	{
		_definition = GameSession.SelectedLevel is { } selected && selected.ProgressLevel == LevelNumber
			? selected : DefaultDefinition ?? throw new InvalidOperationException("关卡没有配置 LevelDefinition。");
		_selectedSpawnSpeed = ReferenceEquals(_definition, GameSession.SelectedLevel) ? GameSession.SelectedSpawnSpeed : 1;
		if (_definition.MapScene is null) throw new InvalidOperationException($"{_definition.Id} 缺少地图场景。");
		var map = _definition.MapScene.Instantiate<Node2D>();
		map.Name = "Map";
		AddChild(map);
		MoveChild(map, 0);
		(_character, _inventory) = GameSessionCharacter.Prepare(ItemCatalog);
		var role = SkillCatalogRegistry.Default.Get(_character.Id);
		Player existing = GetNode<Player>("Player");
		if (existing.SceneFilePath != role.ActorScene.ResourcePath)
		{
			Vector2 spawn = existing.Position;
			RemoveChild(existing);
			existing.Free();
			var actor = role.ActorScene.Instantiate<Player>();
			actor.Name = "Player";
			actor.Position = spawn;
			actor.BindCharacter(_character, ItemCatalog);
			AddChild(actor);
		}
		else existing.BindCharacter(_character, ItemCatalog);
	}

	public override void _Ready()
	{
		_player = GetNode<Player>("Player");
		_statistics = new LevelRunStatistics(_player);
		_player.ProgressionChanged += SaveProgression;
		_player.SoulsCollected += CollectSouls;
		_equipmentBinding = new(_character, ItemCatalog, _inventory, Events,
			_player.RefreshCharacterStats, () => GameSession.Save(_inventory), GameSession.Data!.Wallet);
		_camera = GetNode<Camera2D>("Camera2D");
		_pool = new MonsterPool(GetNode("Enemies"));
		_inventoryAdapter = new InventoryViewAdapter(_inventory, ItemCatalog);
		var backpack = GetNode<Node2D>("HUD/BackPack");
		_backpackView = new LegacyBackpackView(backpack, _inventoryAdapter, ItemCatalog,
			_character, GameSession.Data!.Wallet, _player, () => GameSession.Save(_inventory), Events);
		var menus = GetNode<MenuManager>("MenuManager");
		menus.RegisterMenu("bag", backpack);
		_backpackView.CloseRequested += menus.CloseMenu;
		_status = GetNode<Label>("HUD/Status");
		var oldHud = GetNode<Node2D>("OldHud");
		var skills = new SkillLearningService(_character, GameSession.Data!.Wallet);
		var skillRoot = GD.Load<PackedScene>("res://Scenes/UI/Skill/Learn_skill.tscn").Instantiate<Node2D>();
		GetNode("HUD").AddChild(skillRoot);
		menus.RegisterMenu("skills_menu", skillRoot);
		var skillPanel = new LegacySkillPanel();
		skillPanel.Bind(skillRoot, skills, () => GameSession.Save(_inventory), _player);
		skillRoot.AddChild(skillPanel);
		skillPanel.CloseRequested += menus.CloseMenu;
		if (!InputMap.HasAction("pause_menu"))
		{
			InputMap.AddAction("pause_menu");
			InputMap.ActionAddEvent("pause_menu", new InputEventKey { PhysicalKeycode = Key.Escape });
		}
		var settings = GD.Load<PackedScene>("res://Scenes/UI/Settings/SetMenu.tscn").Instantiate<Control>();
		GetNode("HUD").AddChild(settings);
		menus.RegisterMenu("pause_menu", settings);
		var settingsPanel = new LegacySettingsPanel();
		settingsPanel.Bind(settings, menus, () => GameSession.Save(_inventory));
		settings.AddChild(settingsPanel);
		var playerHud = new PlayerHud();
		playerHud.Bind(oldHud, _player, skills);
		oldHud.AddChild(playerHud);
		menus.MenuStateChanged += isOpen =>
		{
			_status.Visible = !isOpen;
			oldHud.GetNode<CanvasLayer>("roleLayer").Visible = !isOpen;
		};
		_advancePrompt = oldHud.GetNode<AnimatedSprite2D>("roleLayer/Gogo");
		_advancePrompt.Hide();
		var combo = new ComboHud();
		_combo = combo;
		combo.Bind(_player, oldHud.GetNode<CanvasLayer>("roleLayer"));
		oldHud.AddChild(combo);
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/backpack").Pressed += () => menus.ToggleMenu("bag");
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/set").Pressed += () => menus.ToggleMenu("pause_menu");
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/skill").Pressed += () => menus.ToggleMenu("skills_menu");
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/magic_weapon").Pressed += () => menus.ToggleMenu("bag");
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/pet").Disabled = true;
		if (_definition.WaveEncounter is { } waves)
		{
			_forest = new ForestEncounter();
			_forest.Configure(this, _player, _pool, waves, _definition.DisplayName, _selectedSpawnSpeed);
			_forest.MonsterDefeated += AwardLoot;
			_forest.Cleared += () => { _exitPending = true; _exitDelay = 2; };
			AddChild(_forest);
			_bossHud = new BossHud();
			_bossHud.Bind(oldHud.GetNode<CanvasLayer>("roleLayer"));
			oldHud.AddChild(_bossHud);
		}
		if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--level-bag-test"))
			CallDeferred(MethodName.StartBagTest);
	}

	private void StartBagTest() => AddChild(new GameplayBackpackSmokeTest());

	public override void _ExitTree()
	{
		if (_player is not null) _player.ProgressionChanged -= SaveProgression;
		if (_player is not null) _player.SoulsCollected -= CollectSouls;
		_backpackView?.Dispose();
		_inventoryAdapter?.Dispose();
		_equipmentBinding?.Dispose();
		_statistics?.Dispose();
		GetTree().Paused = false;
	}

	public override void _Process(double delta)
	{
		if (_transitioning) return;
		float step = (float)delta;
		UpdateCamera();
		if (_ending) return;
		_statistics.Tick(delta);
		if (_player.IsDead) { BeginEnding(false); return; }
		_advancePrompt.Visible = _forest?.NeedsAdvance == true && !_player.IsDead;
		_advancePrompt.Position = new(GetViewport().GetVisibleRect().Size.X - 70, 259);
		if (_forest is null) UpdateMonsters(step);
		_bossHud?.Track(_forest?.Boss);
		_status.Text = _forest?.Status ?? $"{LevelName}   小怪 {_active.Count}/{MaximumMonsters}";
		if (_forest is null && _player.Position.X > 4600) BeginEnding(true);
	}

	private void UpdateCamera()
	{
		if (_forest is null)
		{
			_camera.Position = new(Mathf.Clamp(_player.Position.X, 480, 4700), 295);
			return;
		}
		_camera.LimitRight = _forest.CameraRight;
		_camera.LimitBottom = 590;
		// 可视宽度会随窗口比例变化，不能把半屏宽度固定写成480。
		float halfWidth = GetViewport().GetVisibleRect().Size.X / (2 * _camera.Zoom.X);
		_camera.Position = new(Mathf.Clamp(_player.Position.X, halfWidth, _forest.CameraRight - halfWidth), 295);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_exitPending && !_ending && !_player.IsDead)
		{
			_exitDelay -= (float)delta;
			if (_exitDelay <= 0) OpenExit();
		}
		if (_ending)
		{
			_endDelay -= (float)delta;
			// 死亡特效独立播放，不能只按身体动画结束时间截断墓碑下落。
			if (_result is not null && _endDelay <= 0 && (_result.Victory ||
				(!_player.Animator.IsPlaying() && !_player.GetNode<AnimatedSprite2D>("Facing/Visual/Death").IsPlaying()))) ShowSettlement();
			return;
		}
		if (_forest is not null || _transitioning || _player.IsDead || _active.Count >= MaximumMonsters) return;
		_spawnClock += (float)delta;
		if (_spawnClock < SpawnInterval / _selectedSpawnSpeed) return;
		_spawnClock = 0;
		SpawnRandomMonster();
	}

	private string LevelName => _definition.DisplayName;

	private void SaveProgression() => GameSession.Save(_inventory);

	private void CollectSouls(int amount)
	{
		GameSession.Data!.Wallet.Add(CurrencyType.Soul, amount);
		GameSession.Save(_inventory);
	}

	private void UpdateMonsters(float delta)
	{
		foreach (var entry in new List<KeyValuePair<Monster, (int Kind, float CorpseTime)>>(_active))
		{
			Monster monster = entry.Key;
			if (Mathf.Abs(monster.GlobalPosition.X - _player.GlobalPosition.X) > 1000)
			{
				_active.Remove(monster);
				_pool.Release(entry.Value.Kind, monster);
				continue;
			}
			if (!monster.IsDead) continue;
			if (_lootAwarded.Add(monster)) AwardLoot(monster);
			float time = entry.Value.CorpseTime + delta;
			if (time >= 1)
			{
				_active.Remove(monster);
				_pool.Release(entry.Value.Kind, monster);
			}
			else _active[monster] = (entry.Value.Kind, time);
		}
	}

	private void SpawnRandomMonster()
	{
		// 旧关卡按路段逐步加入新小怪；这里随机选同一路段的种类，不生成 Boss。
		float progress = _player.Position.X;
		int kind = LevelNumber switch
		{
			1 when progress < 1600 => 1,
			1 when progress < 2800 => GD.Randf() < 0.4f ? 1 : 2,
			1 when progress < 4000 => 2,
			1 => GD.Randf() < 0.8f ? 2 : 3,
			2 when progress < 1600 => 1,
			_ => GD.Randf() < 0.5f ? 2 : 3
		};
		for (int attempt = 0; attempt < 12; attempt++)
		{
			float direction = GD.Randf() < 0.8f ? 1 : -1;
			float x = Mathf.Clamp(_player.Position.X + direction * (350 + GD.Randf() * 300), 250, 4450);
			if (!TryFindSpawnPosition(x, out Vector2 position)) continue;
			Monster monster = _pool.Spawn(kind, position);
			_lootAwarded.Remove(monster);
			_active[monster] = (kind, 0);
			return;
		}
	}

	private bool TryFindSpawnPosition(float x, out Vector2 position)
	{
		position = default;
		var space = GetWorld2D().DirectSpaceState;
		var ray = PhysicsRayQueryParameters2D.Create(new Vector2(x, 240), new Vector2(x, 700), 1);
		var groundHit = space.IntersectRay(ray);
		if (groundHit.Count == 0) return false;
		Vector2 ground = groundHit["position"].AsVector2();
		Vector2 normal = groundHit["normal"].AsVector2();
		// 旧关卡有斜坡和高墙，拒绝陡面及高墙顶面的落点。
		if (ground.Y is < 370 or > 560 || normal.Dot(Vector2.Up) < 0.7f) return false;
		var playerRay = PhysicsRayQueryParameters2D.Create(
			_player.GlobalPosition + new Vector2(0, -50), new Vector2(_player.GlobalPosition.X, 700), 1);
		var playerGroundHit = space.IntersectRay(playerRay);
		if (playerGroundHit.Count == 0 ||
			Mathf.Abs(playerGroundHit["position"].AsVector2().Y - ground.Y) > 35) return false;
		position = new Vector2(x, ground.Y - 3);
		// 出生点即使不与墙重叠，也不能隔着斜坡或挡墙追击玩家。
		var path = PhysicsRayQueryParameters2D.Create(
			_player.GlobalPosition + new Vector2(0, -35), position + new Vector2(0, -35), 1);
		if (space.IntersectRay(path).Count > 0) return false;
		var clearance = new PhysicsShapeQueryParameters2D
		{
			Shape = _spawnClearance,
			Transform = new Transform2D(0, position + new Vector2(0, -32)),
			CollisionMask = 1,
			CollideWithBodies = true
		};
		return space.IntersectShape(clearance, 1).Count == 0;
	}

	private void BeginEnding(bool victory)
	{
		if (_ending || _transitioning) return;
		victory &= !_player.IsDead;
		_ending = true;
		_endDelay = victory ? (_exit is null ? 2 : 0) : 0.25f;
		_result = _statistics.Finish(victory, LevelName, _combo.MaximumCount);
		_statistics.Dispose();
		_player.InputEnabled = false;
		var menus = GetNode<MenuManager>("MenuManager");
		menus.CloseMenu();
		menus.SetProcessInput(false);
		_forest?.SetPhysicsProcess(false);
		_advancePrompt.Hide();
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
			if (node is Monster monster) { monster.AiEnabled = false; monster.SetPhysicsProcess(false); }
		if (victory) GameSession.CompleteLevel(_definition.ProgressLevel, _inventory);
		else GameSession.Save(_inventory);
	}
	private void OpenExit()
	{
		_exitPending = false;
		_exit = GD.Load<PackedScene>("res://Scenes/LevelExit.tscn").Instantiate<LevelExit>();
		_exit.Position = _definition.WaveEncounter!.ExitPosition;
		_exit.Bind(_player);
		_exit.Activated += () => BeginEnding(true);
		AddChild(_exit);
	}
	private void ShowSettlement()
	{
		SetPhysicsProcess(false);
		GetNode<Node2D>("OldHud").GetNode<CanvasLayer>("roleLayer").Hide();
		_status.Hide();
		var settlement = new LevelSettlement { Name = "Settlement" };
		AddChild(settlement);
		settlement.ShowResult(_result!);
		settlement.ReturnRequested += () => Navigate(GameSession.FirstMap);
		settlement.RetryRequested += () => Navigate(SceneFilePath);
		GetTree().Paused = true;
	}
	private void Navigate(string path)
	{
		if (_transitioning) return;
		_transitioning = true;
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(path);
	}

	private void AwardLoot(Monster monster)
	{
		string[] pool = _definition.CommonDropIds;
		if (monster.IsBoss)
		{
			foreach (string id in _definition.BossDropIds)
				if (!_inventory.AddItem(id, 1)) _status.Text = "背包已满，无法领取 Boss 装备";
		}
		else if (pool.Length > 0 && GD.Randf() < 0.18f) _inventory.AddItem(pool[GD.RandRange(0, pool.Length - 1)], 1);
		GameSession.Save(_inventory);
	}


}
