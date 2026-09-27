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
	private float _spawnClock;
	private bool _transitioning;

	public override void _EnterTree()
	{
		(_character, _inventory) = GameSessionCharacter.Prepare(ItemCatalog);
		float maxHealth = _character.BaseStats.MaxHealth + _character.PermanentBonuses.MaxHealth;
		if (maxHealth > 0)
			GetNode<Player>("Player").MaxHealth = maxHealth;
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
		_spawnClock += step;
		if (_spawnClock >= SpawnInterval && !_player.IsDead && _active.Count < MaximumMonsters)
		{
			_spawnClock = 0;
			SpawnRandomMonster();
		}
		_status.Text = $"{LevelName}  {_player.Health:0}/{_player.MaxHealth:0}   小怪 {_active.Count}/{MaximumMonsters}"
			+ (_player.IsDead ? "   按 R 重试" : "");
		UpdateOldHud();
		if (_player.Position.X > 4600 && !_player.IsDead) CompleteLevel();
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
		float direction = GD.Randf() < 0.8f ? 1 : -1;
		float x = Mathf.Clamp(_player.Position.X + direction * (350 + GD.Randf() * 300), 250, 4450);
		Monster monster = _pool.Spawn(kind, new Vector2(x, 490));
		_active[monster] = (kind, 0);
	}

	private void CompleteLevel()
	{
		_transitioning = true;
		if (LevelNumber == 3)
		{
			GameSession.Save(_inventory);
			_status.Text = "前三关已完成";
			_player.InputEnabled = false;
			foreach (var entry in _active)
				_pool.Release(entry.Value.Kind, entry.Key);
			_active.Clear();
			var returnButton = new Button
			{
				Text = "返回主菜单",
				Position = new Vector2(380, 250),
				Size = new Vector2(180, 56)
			};
			returnButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/MainMenu.tscn");
			GetNode("HUD").AddChild(returnButton);
			return;
		}
		string next = $"res://Scenes/Level/Level_{LevelNumber + 1}.tscn";
		GameSession.Save(_inventory);
		GetTree().ChangeSceneToFile(next);
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
