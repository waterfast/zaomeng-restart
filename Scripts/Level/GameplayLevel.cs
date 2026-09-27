using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.UI;
using Zaomeng.UI.Inventory;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Level;

/// <summary>前三关共享的 C# 运行时；地图画面、怪物种类由场景配置。</summary>
public partial class GameplayLevel : Node2D
{
	[Export(PropertyHint.Range, "1,3,1")] public int LevelNumber { get; set; } = 1;
	[Export] public ItemCatalog ItemCatalog { get; set; } = null!;
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
	private Label _status = null!;
	private readonly CapsuleShape2D _spawnClearance = new() { Radius = 18, Height = 60 };
	private float _spawnClock;
	private bool _transitioning;

	public override void _EnterTree()
	{
		(_character, _inventory) = GameSessionCharacter.Prepare(ItemCatalog);
		GetNode<Player>("Player").BindCharacter(_character, ItemCatalog);
	}

	public override void _Ready()
	{
		_player = GetNode<Player>("Player");
		_camera = GetNode<Camera2D>("Camera2D");
		_pool = new MonsterPool(GetNode("Enemies"));
		_inventoryAdapter = new InventoryViewAdapter(_inventory, ItemCatalog);
		var backpack = GetNode<Node2D>("HUD/BackPack");
		_backpackView = new LegacyBackpackView(backpack, _inventoryAdapter, ItemCatalog,
			_character, GameSession.Data!.Wallet, _player, () => GameSession.Save(_inventory));
		var menus = GetNode<MenuManager>("MenuManager");
		menus.RegisterMenu("bag", backpack);
		_backpackView.CloseRequested += menus.CloseMenu;
		_status = GetNode<Label>("HUD/Status");
		var oldHud = GetNode<Node2D>("OldHud");
		menus.MenuStateChanged += isOpen =>
		{
			_status.Visible = !isOpen;
			oldHud.GetNode<CanvasLayer>("roleLayer").Visible = !isOpen;
		};
		oldHud.GetNode<AnimatedSprite2D>("roleLayer/Gogo").Hide();
		oldHud.GetNode<Label>("roleLayer/role_hp_mp_exp/role_level").Text = _character.Level.ToString();
		oldHud.GetNode<BaseButton>("roleLayer/role_menu/backpack").Pressed += () => menus.ToggleMenu("bag");
		foreach (string name in new[] { "set", "skill", "magic_weapon", "pet" })
			oldHud.GetNode<BaseButton>($"roleLayer/role_menu/{name}").Disabled = true;
		if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--level-bag-test"))
			CallDeferred(MethodName.StartBagTest);
	}

	private void StartBagTest() => AddChild(new GameplayBackpackSmokeTest());

	public override void _ExitTree()
	{
		_backpackView?.Dispose();
		_inventoryAdapter?.Dispose();
	}

	public override void _Process(double delta)
	{
		if (_transitioning) return;
		float step = (float)delta;
		_camera.Position = new Vector2(Mathf.Clamp(_player.Position.X, 480, 4700), 280);
		UpdateMonsters(step);
		_status.Text = $"{LevelName}  {_player.Health:0}/{_player.MaxHealth:0}   小怪 {_active.Count}/{MaximumMonsters}"
			+ (_player.IsDead ? "   按 R 重试" : "");
		UpdateOldHud();
		if (_player.Position.X > 4600 && !_player.IsDead) CompleteLevel();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_transitioning || _player.IsDead || _active.Count >= MaximumMonsters) return;
		_spawnClock += (float)delta;
		if (_spawnClock < SpawnInterval) return;
		_spawnClock = 0;
		SpawnRandomMonster();
	}

	private string LevelName => LevelNumber switch { 1 => "花果山", 2 => "水帘洞", _ => "桃花源" };

	private void UpdateOldHud()
	{
		var hud = GetNode<Node2D>("OldHud");
		var hp = hud.GetNode<TextureProgressBar>("roleLayer/role_hp_mp_exp/hp_bar");
		hp.MaxValue = _player.MaxHealth;
		hp.Value = _player.Health;
		hud.GetNode<Label>("roleLayer/role_hp_mp_exp/hp_bar/hp_text").Text = $"{_player.Health:0}/{_player.MaxHealth:0}";
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

	private void CompleteLevel()
	{
		_transitioning = true;
		GameSession.CompleteLevel(LevelNumber, _inventory);
		GetTree().ChangeSceneToFile(GameSession.FirstMap);
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (_player.IsDead && input.IsActionPressed("restart"))
		{
			GetTree().ReloadCurrentScene();
			GetViewport().SetInputAsHandled();
		}
	}

}
