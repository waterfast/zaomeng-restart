using Godot;
using System;
using Zaomeng.Items;
using Zaomeng.Inventory;
using Zaomeng.UI;
using Zaomeng.UI.Inventory;
using Zaomeng.Save;
using Zaomeng.Equipment;
using Zaomeng.Events;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

public partial class TestArena : Node2D
{
	[Export] public ItemCatalog ItemCatalog { get; set; } = null!;
	[Export] public GameplayEvents Events { get; set; } = null!;

	private Player _player = null!;
	private Monster _monster = null!;
	private Label _status = null!;
	private Node2D _backpack = null!;
	private MenuManager _menuManager = null!;
	private InventoryViewAdapter _inventoryAdapter = null!;
	private LegacyBackpackView _backpackView = null!;
	private MagicWeaponView _magicWeaponView = null!;
	private InventoryService _inventory = null!;
	private SaveCharacter _character = null!;
	private GameplayEquipmentBinding _equipmentBinding = null!;

	public override void _EnterTree()
	{
		(_character, _inventory) = GameSessionCharacter.Prepare(ItemCatalog);
		GetNode<Player>("Player").BindCharacter(_character, ItemCatalog);
	}

	public override void _Ready()
	{
		_player = GetNode<Player>("Player");
		_player.ProgressionChanged += SaveProgression;
		_player.SoulsCollected += CollectSouls;
		_equipmentBinding = new(_character, ItemCatalog, _inventory, Events,
			_player.RefreshCharacterStats, () => GameSession.Save(_inventory), GameSession.Data!.Wallet);
		_monster = GetNode<Monster>("Monster");
		_status = GetNode<Label>("HUD/Panel/Status");
		_backpack = GetNode<Node2D>("HUD/BackPack");
		_menuManager = GetNode<MenuManager>("MenuManager");
		_menuManager.MenuStateChanged += isOpen => GetNode<ColorRect>("HUD/Panel").Visible = !isOpen;
		_inventoryAdapter = new InventoryViewAdapter(_inventory, ItemCatalog);
		_magicWeaponView = new MagicWeaponView(GetNode("HUD"), ItemCatalog, _character, Events);
		_menuManager.RegisterMenu("artifacts_menu", _magicWeaponView.Root);
		_magicWeaponView.CloseRequested += _menuManager.CloseMenu;
		_backpackView = new LegacyBackpackView(_backpack, _inventoryAdapter, ItemCatalog,
			_character, GameSession.Data!.Wallet, _player, () => GameSession.Save(_inventory), Events,
			() => _menuManager.ToggleMenu("artifacts_menu"));
		_menuManager.RegisterMenu("bag", _backpack);
		_backpackView.CloseRequested += _menuManager.CloseMenu;
		var hud = GD.Load<PackedScene>("res://Scenes/UI/Level/Role_information.tscn").Instantiate<Node2D>();
		hud.Name = "PlayerHud";
		AddChild(hud);
		var playerHud = new PlayerHud();
		playerHud.Bind(hud, _player);
		hud.AddChild(playerHud);
		hud.GetNode<AnimatedSprite2D>("roleLayer/Gogo").Hide();
		hud.GetNode<BaseButton>("roleLayer/role_menu/backpack").Pressed += () => _menuManager.ToggleMenu("bag");
		hud.GetNode<BaseButton>("roleLayer/role_menu/magic_weapon").Pressed += () => _menuManager.ToggleMenu("artifacts_menu");
		foreach (string name in new[] { "set", "skill", "pet" })
			hud.GetNode<BaseButton>($"roleLayer/role_menu/{name}").Disabled = true;
		_menuManager.MenuStateChanged += isOpen => hud.GetNode<CanvasLayer>("roleLayer").Visible = !isOpen;
		if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--smoke-test"))
			CallDeferred(MethodName.StartSmokeTest);
		if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--menu-pause-test"))
			CallDeferred(MethodName.StartMenuPauseTest);
		if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--equipment-architecture-test"))
			CallDeferred(MethodName.StartEquipmentArchitectureTest);
	}

	public override void _ExitTree()
	{
		if (_player is not null) _player.ProgressionChanged -= SaveProgression;
		if (_player is not null) _player.SoulsCollected -= CollectSouls;
		_backpackView?.Dispose();
		_magicWeaponView?.Dispose();
		_inventoryAdapter?.Dispose();
		_equipmentBinding?.Dispose();
	}

	private void SaveProgression() => GameSession.Save(_inventory);

	private void CollectSouls(int amount)
	{
		GameSession.Data!.Wallet.Add(CurrencyType.Soul, amount);
		GameSession.Save(_inventory);
	}

	private void StartSmokeTest() => AddChild(new CombatSmokeTest());
	private void StartMenuPauseTest() => AddChild(new MenuPauseSmokeTest());
	private void StartEquipmentArchitectureTest() => AddChild(new EquipmentArchitectureSmokeTest());

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
