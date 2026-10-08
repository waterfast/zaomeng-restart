using System;
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

/// <summary>关卡装配与结算；地形、边界和遭遇全部读取资源。</summary>
public partial class GameplayLevel : Node2D
{
	[Export] public int LevelNumber { get; set; } = 1;
	[Export] public ItemCatalog ItemCatalog { get; set; } = null!;
	[Export] public GameplayEvents Events { get; set; } = null!;
	[Export] public LevelDefinition? DefaultDefinition { get; set; }

	private readonly RandomNumberGenerator _dropRandom = new();
	private Player _player = null!;
	private Camera2D _camera = null!;
	private MonsterPool _pool = null!;

	private InventoryService _inventory = null!;
	private InventoryViewAdapter _inventoryAdapter = null!;
	private LegacyBackpackView _backpackView = null!;
	private MagicWeaponView _magicWeaponView = null!;
	private SaveCharacter _character = null!;
	private GameplayEquipmentBinding _equipmentBinding = null!;

	private bool _transitioning;
	private WaveEncounter? _encounter;
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

	private LevelDefinition _definition = null!;
	private LevelDifficultyDefinition _difficulty = null!;
	private float _selectedSpawnSpeed = 1;

	public override void _EnterTree()
	{
		_definition = GameSession.SelectedLevel is { } selected && selected.LevelScenePath == SceneFilePath
			? selected : DefaultDefinition ?? throw new InvalidOperationException("关卡没有配置 LevelDefinition。");
		_selectedSpawnSpeed = ReferenceEquals(_definition, GameSession.SelectedLevel) ? GameSession.SelectedSpawnSpeed : 1;
		if (_definition.MapScene is null) throw new InvalidOperationException($"{_definition.Id} 缺少地图场景。");
		_definition.Validate(ItemCatalog);
		_difficulty = ReferenceEquals(_definition, GameSession.SelectedLevel)
			? GameSession.SelectedDifficulty ?? throw new InvalidOperationException("挑战没有选择模式。")
			: _definition.Difficulties[0];
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
		GetNode<Player>("Player").Position = _definition.PlayerSpawn;
	}

	public override void _Ready()
	{
		Zaomeng.Audio.AudioManager.Instance?.EnterLevel(_definition.Music);
		_dropRandom.Randomize();
		_player = GetNode<Player>("Player");
		if (GameSession.TakeEntranceVitals(_definition.Id) is { } vitals)
		{
			_player.RestoreEntranceVitals(vitals.Health, vitals.Mana);
		}
		_statistics = new LevelRunStatistics(_player);
		_player.ProgressionChanged += SaveProgression;
		_player.SoulsCollected += CollectSouls;
		_equipmentBinding = new(_character, ItemCatalog, _inventory, Events,
			_player.RefreshCharacterStats, () => GameSession.Save(_inventory), GameSession.Data!.Wallet, () => _player);
		_camera = GetNode<Camera2D>("Camera2D");
		_pool = new MonsterPool(GetNode("Enemies"), _difficulty);
		_inventoryAdapter = new InventoryViewAdapter(_inventory, ItemCatalog);
		var backpack = GetNode<Node2D>("HUD/BackPack");
		var menus = GetNode<MenuManager>("MenuManager");
		_magicWeaponView = new MagicWeaponView(GetNode("HUD"), ItemCatalog, _character, Events);
		menus.RegisterMenu("artifacts_menu", _magicWeaponView.Root);
		_magicWeaponView.CloseRequested += menus.CloseMenu;
		_backpackView = new LegacyBackpackView(backpack, _inventoryAdapter, ItemCatalog,
			_character, GameSession.Data!.Wallet, _player, () => GameSession.Save(_inventory), Events,
			() => menus.ToggleMenu("artifacts_menu"));
		menus.RegisterMenu("bag", backpack);
		_backpackView.CloseRequested += menus.CloseMenu;
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
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/magic_weapon").Pressed += () => menus.ToggleMenu("artifacts_menu");
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/pet").Disabled = true;
		if (_definition.WaveEncounter is { } waves)
		{
			_encounter = new WaveEncounter();
			_encounter.Configure(this, _player, _pool, waves, _definition.DisplayName, _selectedSpawnSpeed);
			_encounter.MonsterDefeated += AwardLoot;
			_encounter.Cleared += () => { _exitPending = true; _exitDelay = 2; };
			AddChild(_encounter);
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
		_magicWeaponView?.Dispose();
		_inventoryAdapter?.Dispose();
		_equipmentBinding?.Dispose();
		_statistics?.Dispose();
		GetTree().Paused = false;
	}

	public override void _Process(double delta)
	{
		if (_transitioning) return;
		UpdateCamera();
		if (_ending) return;
		_statistics.Tick(delta);
		if (_player.IsDead) { BeginEnding(false); return; }
		_advancePrompt.Visible = _encounter?.NeedsAdvance == true && !_player.IsDead;
		_advancePrompt.Position = new(GetViewport().GetVisibleRect().Size.X - 70, 259);

		_bossHud?.Track(_encounter?.Boss);
		if (_encounter is null && _exit is null && !_exitPending && _player.Position.X >= _definition.ExitPosition.X - 100)
		{ _exitPending = true; _exitDelay = 0; }
	}

	private void UpdateCamera()
	{
		if (_encounter is null)
		{
			float half = GetViewport().GetVisibleRect().Size.X / (2 * _camera.Zoom.X);
			_camera.LimitRight = _definition.CameraRight;
			_camera.LimitBottom = _definition.CameraBottom;
			_camera.Position = new(Mathf.Clamp(_player.Position.X, half, Mathf.Max(half, _definition.CameraRight - half)), _definition.CameraBottom / 2f);
			return;
		}
		_camera.LimitRight = _encounter.CameraRight;
		_camera.LimitBottom = _definition.CameraBottom;
		// 可视宽度会随窗口比例变化，不能把半屏宽度固定写成480。
		float halfWidth = GetViewport().GetVisibleRect().Size.X / (2 * _camera.Zoom.X);
		_camera.Position = new(Mathf.Clamp(_player.Position.X, halfWidth, Mathf.Max(halfWidth, _encounter.CameraRight - halfWidth)), _definition.CameraBottom / 2f);
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

	}

	private string LevelName => _definition.DisplayName;

	private void SaveProgression() => GameSession.Save(_inventory);

	private void CollectSouls(int amount)
	{
		GameSession.Data!.Wallet.Add(CurrencyType.Soul, amount);
		GameSession.Save(_inventory);
	}

	private void BeginEnding(bool victory)
	{
		if (_ending || _transitioning) return;
		victory &= !_player.IsDead;
		_ending = true;
		Zaomeng.Audio.AudioManager.Instance?.PlayResult(victory);
		_endDelay = victory ? (_exit is null ? 2 : 0) : 0.25f;
		_result = _statistics.Finish(victory, LevelName, _combo.MaximumCount);
		_statistics.Dispose();
		_player.InputEnabled = false;
		var menus = GetNode<MenuManager>("MenuManager");
		menus.CloseMenu();
		menus.SetProcessInput(false);
		_encounter?.SetPhysicsProcess(false);
		foreach (Node child in GetChildren())
			if (child is ItemPickup pickup) pickup.SetPhysicsProcess(false);
		_advancePrompt.Hide();
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
			if (node is Monster monster) { monster.AiEnabled = false; monster.SetPhysicsProcess(false); }
		if (victory && _definition.AdvancesCampaign) GameSession.CompleteLevel(_definition.ProgressLevel, _inventory);
		else GameSession.Save(_inventory);
	}
	private void OpenExit()
	{
		_exitPending = false;
		_exit = GD.Load<PackedScene>("res://Scenes/LevelExit.tscn").Instantiate<LevelExit>();
		// 配置表达地面脚点；允许局部地形微调，避免沿用旧图中心坐标把光圈埋进地里。
		Vector2 hint = _definition.ExitPosition;
		var query = PhysicsRayQueryParameters2D.Create(ToGlobal(hint + new Vector2(0, -160)),
			ToGlobal(hint + new Vector2(0, 160)), 1);
		var ground = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (ground.Count == 0 || ground["normal"].AsVector2().Dot(Vector2.Up) < 0.7f)
			throw new InvalidOperationException($"{_definition.Id} 出口下方没有可站立地面：{hint}");
		_exit.Position = ToLocal(ground["position"].AsVector2());
		_exit.Bind(_player);
		_exit.Activated += () => BeginEnding(true);
		AddChild(_exit);
	}
	private void ShowSettlement()
	{
		SetPhysicsProcess(false);
		GetNode<Node2D>("OldHud").GetNode<CanvasLayer>("roleLayer").Hide();
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

	public bool EnterHiddenLevel(LevelDefinition destination)
	{
		if (_ending || _transitioning || _player.IsDead) return false;
		var previousLevel = GameSession.SelectedLevel;
		var previousDifficulty = GameSession.SelectedDifficulty;
		float previousSpeed = GameSession.SelectedSpawnSpeed;
		try
		{
			destination.Validate(ItemCatalog);
			GameSession.Save(_inventory);
			var difficulty = destination.Difficulties.Contains(_difficulty) ? _difficulty : destination.Difficulties[0];
			GameSession.SelectLevel(destination, _selectedSpawnSpeed, difficulty);
			GameSession.SetEntranceVitals(destination.Id, _player.Health, _player.Mana);
			Error error = GetTree().ChangeSceneToFile(destination.LevelScenePath);
			if (error != Error.Ok) throw new InvalidOperationException($"隐藏关卡切换失败：{error}");
			_transitioning = true;
			return true;
		}
		catch (Exception error)
		{
			GameSession.ClearEntranceVitals();
			if (previousLevel is not null && previousDifficulty is not null)
				GameSession.SelectLevel(previousLevel, previousSpeed, previousDifficulty);
			else GameSession.ClearLevelSelection();
			GD.PushError(error.ToString());
			return false;
		}
	}

	private void AwardLoot(Monster monster)
	{
		if (!monster.GrantsRewards) return;
		if (_ending || _transitioning || _player.IsDead) return;
		if (monster.Definition is not { } definition) return;
		var scene = GD.Load<PackedScene>("res://Scenes/Effects/ItemPickup.tscn");
		int index = 0;
		foreach (var drops in _definition.GetDropTables(definition, _difficulty))
			foreach (var (id, count) in drops.Roll(_dropRandom))
			{
				if (!ItemCatalog.TryGetDefinition(id, out var item))
					throw new InvalidOperationException($"掉落物品未注册：{id}");
				for (int remaining = count; remaining > 0;)
				{
					int amount = Math.Min(remaining, item!.MaxStack);
					remaining -= amount;
					var pickup = scene.Instantiate<ItemPickup>();
					pickup.Configure(item, amount, _player, CollectItem);
					pickup.Position = ToLocal(monster.GlobalPosition + new Vector2(0, -24));
					// 从击杀位置抛出，交给地形碰撞落地；与怪物池回收无关。
					float direction = index % 2 == 0 ? 1 : -1;
					pickup.Velocity = new(direction * (90 + index / 2 * 100), -180);
					AddChild(pickup);
					index++;
				}
			}
	}

	private bool CollectItem(ItemPickup pickup)
	{
		if (_ending || _transitioning || _player.IsDead || pickup.Collected) return false;
		if (pickup.Item is RecoveryPickupDefinition recovery)
		{
			_player.Heal(_player.MaxHealth * recovery.HealthRatio);
			_player.RestoreMana(_player.MaxMana * recovery.ManaRatio);
			return true;
		}
		if (!_inventory.AddItem(pickup.Item.Id, pickup.Count)) return false;
		// 入包已经提交，写盘失败也不能保留掉落再次领取同一件物品。
		try { GameSession.Save(_inventory); }
		catch (Exception error) { GD.PushError($"物品已拾取，但保存失败：{error}"); }
		return true;
	}

}
