using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;

namespace Zaomeng.UI.MainMenu;

/// <summary>第一张地图只管理入口和存档；旧关卡信息弹窗尚未迁入。</summary>
public partial class Map1 : Node2D
{
	private InventoryService _inventory = null!;

	public override void _Ready()
	{
		var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		(_, _inventory) = GameSessionCharacter.Prepare(catalog);
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
		foreach (string name in new[] { "ldl", "Shop", "Skill_learn", "GameAn", "Task", "level_lhhj" })
			GetNode<TextureButton>(name).Disabled = true;
		GetNode<Button>("next").Hide();
		GetNode<TextureButton>("bc_game").Pressed += () => GameSession.Save(_inventory);
		GetNode<TextureButton>("turn_mainmenu").Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu/MainMenu.tscn");
	}

	private void EnterLevel(int level)
	{
		if (level > GameSession.Data!.UnlockedLevel) return;
		GetTree().ChangeSceneToFile($"res://Scenes/Level/Level_{level}.tscn");
	}
}
