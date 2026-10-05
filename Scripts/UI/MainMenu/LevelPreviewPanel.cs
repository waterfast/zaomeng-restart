using System;
using Godot;
using Zaomeng.Items;
using Zaomeng.Level;

namespace Zaomeng.UI.MainMenu;

/// <summary>旧版关卡弹窗的 C# 交互层；左侧列表和右侧预览只读取入口资源。</summary>
public partial class LevelPreviewPanel : Control
{
	private LevelEntranceDefinition _entrance = null!;
	private ItemCatalog _items = null!;
	private LevelDefinition _selected = null!;
	private float _spawnSpeed = 1;
	private VBoxContainer _tabs = null!;
	public event Action? Closed;
	public event Action<LevelDefinition, float>? ChallengeRequested;

	public void Bind(LevelEntranceDefinition entrance, ItemCatalog items)
	{
		_entrance = entrance;
		_items = items;
	}

	public override void _Ready()
	{
		GetNode<ColorRect>("ColorRect").MouseFilter = MouseFilterEnum.Stop;
		GetNode<Control>("ColorRect2").MouseFilter = MouseFilterEnum.Ignore;
		GetNode<TextureButton>("ColorRect/TextureRect/Close").Pressed += () => Closed?.Invoke();
		GetNode<TextureButton>("ColorRect/TextureRect/Challenge").Pressed += () => ChallengeRequested?.Invoke(_selected, _spawnSpeed);
		GetNode<TextureButton>("ColorRect/TextureRect/Speed").Pressed += () =>
		{
			_spawnSpeed = _spawnSpeed == 1 ? 2 : 1;
			GetNode<Label>("ColorRect/TextureRect/Speed/speedtext").Text = $"×{_spawnSpeed:0}";
		};
		GetNode<Label>("ColorRect/TextureRect/Speed/speedtext").Text = "×1";
		_tabs = new VBoxContainer { Position = new Vector2(130, 105), CustomMinimumSize = new Vector2(180, 0) };
		_tabs.AddThemeConstantOverride("separation", 8);
		GetNode<ColorRect>("ColorRect").AddChild(_tabs);
		foreach (LevelDefinition level in _entrance.Levels)
		{
			var button = new Button { Text = level.DisplayName, CustomMinimumSize = new Vector2(180, 46) };
			button.AddThemeFontSizeOverride("font_size", 20);
			button.Pressed += () => Select(level);
			_tabs.AddChild(button);
		}
		if (_entrance.Levels.Count > 0) Select(_entrance.Levels[0]);
		else GetNode<TextureButton>("ColorRect/TextureRect/Challenge").Disabled = true;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
		{
			Closed?.Invoke();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Select(LevelDefinition level)
	{
		_selected = level;
		GetNode<Label>("ColorRect/TextureRect/Title").Text = $"{level.DisplayName} · {level.Difficulty}";
		GetNode<TextureRect>("ColorRect/TextureRect/Mybg").Texture = level.PreviewBackground;
		GetNode<Label>("ColorRect/TextureRect/Level").Text = "建议等级:";
		GetNode<Label>("ColorRect/TextureRect/Level/LevelDown").Text = level.RecommendedLevel.ToString();
		GetNode<Label>("ColorRect/TextureRect/pj").Text = level.Description;
		GetNode<Label>("ColorRect/TextureRect/pj/MyPj").Hide();
		GetNode<Label>("ColorRect/TextureRect/Fall").Text = "可能掉落：";
		var monsters = GetNode<HBoxContainer>("ColorRect/TextureRect/ScrollContainer2/MonsterList");
		Clear(monsters);
		foreach (LevelMonsterPreview monster in level.Monsters)
		{
			var card = new VBoxContainer { CustomMinimumSize = new Vector2(82, 85) };
			var icon = new TextureRect { Texture = monster.Icon, CustomMinimumSize = new Vector2(62, 62), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
			var name = new Label { Text = monster.IsBoss ? $"Boss {monster.DisplayName}" : monster.DisplayName };
			name.AddThemeFontSizeOverride("font_size", 13);
			card.AddChild(icon);
			card.AddChild(name);
			monsters.AddChild(card);
		}
		var drops = GetNode<HBoxContainer>("ColorRect/TextureRect/ScrollContainer/FallList");
		Clear(drops);
		foreach (string id in level.CommonDropIds) AddDrop(drops, id);
		foreach (string id in level.BossDropIds) AddDrop(drops, id);
		foreach (Node child in _tabs.GetChildren())
			if (child is Button button) button.Disabled = button.Text == level.DisplayName;
	}

	private void AddDrop(HBoxContainer row, string id)
	{
		if (!_items.TryGetDefinition(id, out ItemDefinition? item)) return;
		var icon = new TextureRect
		{
			Texture = item!.Icon,
			TooltipText = item.DisplayName,
			CustomMinimumSize = new Vector2(53, 53),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		row.AddChild(icon);
	}

	private static void Clear(Node node)
	{
		foreach (Node child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
	}
}
