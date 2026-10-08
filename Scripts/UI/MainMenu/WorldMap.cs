using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Skills;
using Zaomeng.Quests;
using Zaomeng.UI.Quests;
using Zaomeng.Level;

namespace Zaomeng.UI.MainMenu;

/// <summary>三张旧地图共用入口控制器，差异由地图资源配置。</summary>
public partial class WorldMap : Node2D
{
	[Export] public WorldMapDefinition Definition { get; set; } = null!;
	private InventoryService _inventory = null!;
	private QuestInteraction? _quests;
	private MenuManager _menus = null!;
	private AcceptDialog _feedback = null!;
	private CanvasLayer _layer = null!;
	private LevelPreviewPanel? _preview;

	public override void _Ready()
	{
		Zaomeng.Audio.AudioManager.Instance?.EnterMenu();
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		var (character, inventory) = GameSessionCharacter.Prepare(catalog);
		_inventory = inventory;
		var layer = new CanvasLayer();
		_layer = layer;
		AddChild(layer);
		_feedback = new AcceptDialog { Title = "地图", ProcessMode = ProcessModeEnum.Always };
		layer.AddChild(_feedback);
		var menus = new MenuManager();
		_menus = menus;
		AddChild(menus);
		// 先禁用没有实现的旧入口，再按配置绑定实际可用按钮。
		foreach (Node node in FindChildren("*", "BaseButton", true, false))
			if (node is BaseButton button) button.Disabled = true;
		var skillRoot = GD.Load<PackedScene>("res://Scenes/UI/Skill/Learn_skill.tscn").Instantiate<Node2D>();
		layer.AddChild(skillRoot);
		menus.RegisterMenu("skills_menu", skillRoot);
		var panel = new LegacySkillPanel();
		panel.Bind(skillRoot, new SkillLearningService(character, GameSession.Data!.Wallet), () => GameSession.Save(_inventory));
		skillRoot.AddChild(panel);
		panel.CloseRequested += menus.CloseMenu;
		BindButton(Definition.SkillButton, () => menus.ToggleMenu("skills_menu"));
		var questCatalog = GD.Load<QuestCatalog>("res://Content/Quests/Registry.tres");
		var questService = new QuestService(questCatalog, character, GameSession.Data!, inventory, catalog);
		_quests = new QuestInteraction(questService, () => GameSession.Save(_inventory));
		var questRoot = GD.Load<PackedScene>("res://Scenes/UI/Task/BasicTask.tscn").Instantiate<Node2D>();
		layer.AddChild(questRoot);
		if (!InputMap.HasAction("quests_menu")) InputMap.AddAction("quests_menu");
		menus.RegisterMenu("quests_menu", questRoot);
		var quests = new LegacyQuestPanel();
		questRoot.AddChild(quests);
		quests.Bind(questRoot, questService, _quests, catalog);
		quests.CloseRequested += menus.CloseMenu;
		BindButton(Definition.QuestButton, () => menus.ToggleMenu("quests_menu"));
		BindTownPanels(layer, menus, catalog, character.Name);
		if (Definition.HasPlayableLevels)
		{
			foreach (LevelEntranceDefinition entrance in Definition.LevelEntrances)
			{
				if (entrance.Levels.Count == 0) continue;
				var button = GetNode<TextureButton>(entrance.ButtonPath);
				button.Disabled = entrance.Levels[0].ProgressLevel > GameSession.Data!.UnlockedLevel;
				button.TooltipText = entrance.Levels[0].DisplayName;
				button.Pressed += () => OpenLevelPreview(entrance, catalog);
			}
		}
		BindButton(Definition.SaveButton, () => TrySave());
		BindButton(Definition.MenuButton, () => ChangeMap("res://Scenes/UI/MainMenu/MainMenu.tscn"));
		if (Definition.NextMap is { } next)
			BindButton(Definition.ForwardButton, () =>
			{
				if (!WorldMapAccess.CanEnter(next, GameSession.Data!)) { ShowFeedback(next.LockedMessage); return; }
				ChangeMap(next.ScenePath);
			});
		BindButton(Definition.BackButton, () => ChangeMap(Definition.PreviousScenePath));
		BindButton(Definition.HomeButton, () => ChangeMap("res://Scenes/UI/MainMenu/Map1.tscn"));
		var feedback = new MapButtonFeedback();
		feedback.Bind(this, Definition.HasPlayableLevels ? GameSession.Data!.UnlockedLevel : 0);
		AddChild(feedback);
	}

	private void BindButton(NodePath path, System.Action action)
	{
		if (path.IsEmpty) return;
		var button = GetNode<BaseButton>(path);
		button.Disabled = false;
		button.Pressed += action;
	}

	private void BindTownPanels(CanvasLayer layer, MenuManager menus, ItemCatalog catalog, string characterName)
	{
		if (!Definition.ShopButton.IsEmpty)
		{
			var root = GD.Load<PackedScene>("res://Scenes/UI/Shop/SHOP.tscn").Instantiate<Node2D>();
			layer.AddChild(root);
			if (!InputMap.HasAction("shop_menu")) InputMap.AddAction("shop_menu");
			menus.RegisterMenu("shop_menu", root);
			var panel = new Zaomeng.UI.Shop.LegacyShopPanel();
			root.AddChild(panel);
			var offers = GD.Load<Zaomeng.Shop.ShopCatalog>("res://GameData/Shop/Registry.tres");
			var wallet = GameSession.Data!.Wallet;
			panel.Bind(root, new Zaomeng.Shop.ShopService(offers, catalog, _inventory, wallet), wallet,
				characterName, () => GameSession.Save(_inventory));
			panel.CloseRequested += menus.CloseMenu;
			BindButton(Definition.ShopButton, () => menus.ToggleMenu("shop_menu"));
		}
		if (!Definition.AlchemyButton.IsEmpty)
		{
			var root = GD.Load<PackedScene>("res://Scenes/UI/LDL/ldl.tscn").Instantiate<Node2D>();
			layer.AddChild(root);
			if (!InputMap.HasAction("alchemy_menu")) InputMap.AddAction("alchemy_menu");
			menus.RegisterMenu("alchemy_menu", root);
			var panel = new Zaomeng.UI.Alchemy.LegacyAlchemyPanel();
			root.AddChild(panel);
			panel.Bind(root, _inventory, catalog, GameSession.Data!.Wallet);
			panel.CloseRequested += menus.CloseMenu;
			BindButton(Definition.AlchemyButton, () => menus.ToggleMenu("alchemy_menu"));
		}
	}
	private bool TrySave()
	{
		try { GameSession.Save(_inventory); return true; }
		catch (System.Exception error) { ShowFeedback($"保存失败：{error.Message}"); return false; }
	}
	private void ChangeMap(string path)
	{
		if (!ResourceLoader.Exists(path)) { ShowFeedback("目标地图尚未配置。"); return; }
		_menus.CloseMenu();
		if (!TrySave()) return;
		Error result = GetTree().ChangeSceneToFile(path);
		if (result != Error.Ok) ShowFeedback($"地图切换失败：{result}");
	}
	private void ShowFeedback(string message) { _feedback.DialogText = message; _feedback.PopupCentered(); }

	private void OpenLevelPreview(LevelEntranceDefinition entrance, ItemCatalog catalog)
	{
		if (_preview is not null) return;
		if (entrance.Levels.Count == 0 || entrance.Levels[0].ProgressLevel > GameSession.Data!.UnlockedLevel) return;
		_menus.CloseMenu();
		_preview = GD.Load<PackedScene>("res://Scenes/UI/MainMenu/LevelInfo.tscn").Instantiate<LevelPreviewPanel>();
		_preview.Bind(entrance, catalog);
		_preview.Closed += CloseLevelPreview;
		_preview.ChallengeRequested += EnterLevel;
		_layer.AddChild(_preview);
	}

	private void CloseLevelPreview()
	{
		_preview?.QueueFree();
		_preview = null;
	}

	private void EnterLevel(LevelDefinition level, float spawnSpeed, LevelDifficultyDefinition difficulty)
	{
		if (level.ProgressLevel > GameSession.Data!.UnlockedLevel) return;
		try
		{
			level.Validate(GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres"));
			GameSession.SelectLevel(level, spawnSpeed, difficulty);
		}
		catch (System.Exception error) { ShowFeedback(error.Message); return; }
		Error result = GetTree().ChangeSceneToFile(level.LevelScenePath);
		if (result != Error.Ok) ShowFeedback($"进入关卡失败：{result}");
	}
	public override void _ExitTree() => _quests?.Dispose();
}
