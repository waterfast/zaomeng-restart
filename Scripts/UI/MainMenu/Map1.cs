using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Skills;
using Zaomeng.Quests;
using Zaomeng.UI.Quests;

namespace Zaomeng.UI.MainMenu;

/// <summary>第一张地图只管理入口和存档；旧关卡信息弹窗尚未迁入。</summary>
public partial class Map1 : Node2D
{
	private InventoryService _inventory = null!;
	private QuestInteraction? _quests;

	public override void _Ready()
	{
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		var (character, inventory) = GameSessionCharacter.Prepare(catalog);
		_inventory = inventory;
		var layer = new CanvasLayer();
		AddChild(layer);
		var menus = new MenuManager();
		AddChild(menus);
		var skillRoot = GD.Load<PackedScene>("res://Scenes/UI/Skill/Learn_skill.tscn").Instantiate<Node2D>();
		layer.AddChild(skillRoot);
		menus.RegisterMenu("skills_menu", skillRoot);
		var panel = new LegacySkillPanel();
		panel.Bind(skillRoot, new SkillLearningService(character, GameSession.Data!.Wallet), () => GameSession.Save(_inventory));
		skillRoot.AddChild(panel);
		panel.CloseRequested += menus.CloseMenu;
		GetNode<TextureButton>("Skill_learn").Pressed += () => menus.ToggleMenu("skills_menu");
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
		GetNode<TextureButton>("Task").Pressed += () => menus.ToggleMenu("quests_menu");
		for (int level = 1; level <= 3; level++)
		{
			int selectedLevel = level;
			var button = GetNode<TextureButton>($"level_{level}");
			button.Disabled = level > GameSession.Data!.UnlockedLevel;
			button.TooltipText = level switch
			{
				1 => "花果山",
				2 => "水帘洞",
				_ => "桃花源"
			};
			button.Pressed += () => EnterLevel(selectedLevel);
		}

		// 旧地图的其余入口还没有新场景或系统，保留画面但不接受点击。
		for (int level = 4; level <= 10; level++)
		{
			string path = level == 9 ? "Node2D2/Node2D/level_9" : $"level_{level}";
			GetNode<TextureButton>(path).Disabled = true;
		}
		foreach (string name in new[] { "ldl", "Shop", "GameAn", "level_lhhj" })
			GetNode<TextureButton>(name).Disabled = true;
		GetNode<Button>("next").Hide();
		GetNode<TextureButton>("bc_game").Pressed += () => GameSession.Save(_inventory);
		GetNode<TextureButton>("turn_mainmenu").Pressed += () =>
		{
			GameSession.Save(_inventory);
			menus.CloseMenu();
			GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/MainMenu.tscn");
		};
		var feedback = new MapButtonFeedback();
		feedback.Bind(this, Mathf.Clamp(GameSession.Data!.UnlockedLevel, 1, 3));
		AddChild(feedback);
	}

	private void EnterLevel(int level)
	{
		if (level > GameSession.Data!.UnlockedLevel) return;
		GetTree().ChangeSceneToFile($"res://Scenes/Level/Level_{level}.tscn");
	}
	public override void _ExitTree() => _quests?.Dispose();
}
