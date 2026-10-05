using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Skills;
using Zaomeng.Quests;
using Zaomeng.UI.Quests;

namespace Zaomeng.UI.MainMenu;

/// <summary>三张旧地图共用入口控制器，差异由地图资源配置。</summary>
public partial class WorldMap : Node2D
{
	[Export] public WorldMapDefinition Definition { get; set; } = null!;
	private InventoryService _inventory = null!;
	private QuestInteraction? _quests;
	private MenuManager _menus = null!;
	private AcceptDialog _feedback = null!;

	public override void _Ready()
	{
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		var (character, inventory) = GameSessionCharacter.Prepare(catalog);
		_inventory = inventory;
		var layer = new CanvasLayer();
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
		if (Definition.HasPlayableLevels)
		{
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
		feedback.Bind(this, Definition.HasPlayableLevels ? Mathf.Clamp(GameSession.Data!.UnlockedLevel, 1, 3) : 0);
		AddChild(feedback);
	}

	private void BindButton(NodePath path, System.Action action)
	{
		if (path.IsEmpty) return;
		var button = GetNode<BaseButton>(path);
		button.Disabled = false;
		button.Pressed += action;
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

	private void EnterLevel(int level)
	{
		if (level > GameSession.Data!.UnlockedLevel) return;
		GetTree().ChangeSceneToFile($"res://Scenes/Level/Level_{level}.tscn");
	}
	public override void _ExitTree() => _quests?.Dispose();
}
