using System;
using System.Linq;
using Zaomeng.UI.Inventory;
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
	private LegacyItemTooltip _tooltip = null!;
	private LevelDifficultyDefinition? _difficulty;
	private HBoxContainer _difficultyButtons = null!;
	private VBoxContainer _tabs = null!;
	public event Action? Closed;
	public event Action<LevelDefinition, float, LevelDifficultyDefinition>? ChallengeRequested;

	public void Bind(LevelEntranceDefinition entrance, ItemCatalog items)
	{
		_entrance = entrance;
		_items = items;
	}

	public override void _Ready()
	{
		var preview = GetNode<Control>("ColorRect/TextureRect");
		preview.Position = new Vector2(Mathf.Max(205, (GetViewportRect().Size.X - 926) / 2 + 200),
			Mathf.Max(12, (GetViewportRect().Size.Y - 474) / 2));
		GetNode<ColorRect>("ColorRect").MouseFilter = MouseFilterEnum.Stop;
		GetNode<Control>("ColorRect2").MouseFilter = MouseFilterEnum.Ignore;
		GetNode<TextureButton>("ColorRect/TextureRect/Close").Pressed += () => Closed?.Invoke();
		GetNode<TextureButton>("ColorRect/TextureRect/Challenge").Pressed += () => { if (_difficulty is not null) ChallengeRequested?.Invoke(_selected, _spawnSpeed, _difficulty); };
		GetNode<TextureButton>("ColorRect/TextureRect/Speed").Pressed += () =>
		{
			_spawnSpeed = _spawnSpeed == 1 ? 2 : 1;
			GetNode<Label>("ColorRect/TextureRect/Speed/speedtext").Text = $"×{_spawnSpeed:0}";
		};
		GetNode<Label>("ColorRect/TextureRect/Speed/speedtext").Text = "×1";
		_tooltip = new LegacyItemTooltip(this);
		var dropScroll = GetNode<ScrollContainer>("ColorRect/TextureRect/ScrollContainer");
		var drag = new HorizontalPreviewScroll();
		drag.Bind(dropScroll, _tooltip.Hide);
		AddChild(drag);
		_difficultyButtons = new HBoxContainer { Name = "DifficultyChoices", Position = new(22, 222) };
		GetNode<Control>("ColorRect/TextureRect").AddChild(_difficultyButtons);
		var caption = new Label { Text = "难度：", MouseFilter = MouseFilterEnum.Ignore };
		caption.AddThemeColorOverride("font_color", Colors.Yellow);
		_difficultyButtons.AddChild(caption);
		_tabs = new VBoxContainer { Name = "LevelChoices", Position = preview.Position + new Vector2(-200, 20), CustomMinimumSize = new Vector2(180, 0) };
		_tabs.AddThemeConstantOverride("separation", 8);
		GetNode<ColorRect>("ColorRect").AddChild(_tabs);
		foreach (LevelDefinition level in _entrance.Levels)
		{
			var button = CreateYellowButton(level.DisplayName, new Vector2(190, 52));
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
		_tooltip.Hide();
		if (_difficulty is null || !level.Difficulties.Contains(_difficulty))
			_difficulty = level.Difficulties.FirstOrDefault(option => option.IsValid);
		foreach (Node child in _difficultyButtons.GetChildren().Skip(1).ToArray())
		{ _difficultyButtons.RemoveChild(child); child.QueueFree(); }
		foreach (LevelDifficultyDefinition option in level.Difficulties)
		{
			if (!option.IsValid) continue;
			var choice = CreateYellowButton(option.DisplayName, new Vector2(100, 36));
			choice.Pressed += () => { _difficulty = option; RefreshDifficulty(); };
			_difficultyButtons.AddChild(choice);
		}
		GetNode<TextureButton>("ColorRect/TextureRect/Challenge").Disabled = _difficulty is null;
		RefreshDifficulty();
		GetNode<TextureRect>("ColorRect/TextureRect/Mybg").Texture = level.PreviewBackground;
		GetNode<Label>("ColorRect/TextureRect/Level").Text = "建议等级:";
		GetNode<Label>("ColorRect/TextureRect/pj").Text = level.Description;
		GetNode<Label>("ColorRect/TextureRect/pj/MyPj").Hide();
		GetNode<Label>("ColorRect/TextureRect/Fall").Text = "可能掉落：";
		var monsters = GetNode<HBoxContainer>("ColorRect/TextureRect/ScrollContainer2/MonsterList");
		Clear(monsters);
		foreach (LevelMonsterPreview monster in level.Monsters)
		{
			var card = new VBoxContainer { CustomMinimumSize = new Vector2(82, 85) };
			var icon = new TextureRect { Texture = monster.Icon, CustomMinimumSize = new Vector2(62, 62), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
			var name = new Label { Text = monster.IsBoss ? $"Boss {monster.DisplayName}" : monster.DisplayName };
			name.AddThemeFontSizeOverride("font_size", 13);
			card.AddChild(icon);
			card.AddChild(name);
			monsters.AddChild(card);
		}
		foreach (Node child in _tabs.GetChildren())
			if (child is TextureButton button) button.SelfModulate = button.GetNode<Label>("Caption").Text == level.DisplayName
				? Colors.White : new Color(0.78f, 0.78f, 0.78f);
	}

	private void AddDrop(HBoxContainer row, string id)
	{
		if (!_items.TryGetDefinition(id, out ItemDefinition? item) || item!.Category == ItemCategory.Consumable) return;
		// 物品图自带旧格子底图，按统一尺寸显示，避免大贴图撑开布局。
		var slot = new TextureRect
		{
			Name = id, Texture = item!.Icon,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			CustomMinimumSize = new Vector2(53, 53),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Pass
		};
		slot.MouseEntered += () => _tooltip.Show(item, 0,
			new Rect2(slot.GetGlobalTransformWithCanvas().Origin, slot.Size * slot.GetGlobalTransformWithCanvas().Scale.Abs()),
			showOwnedCount: false);
		slot.MouseExited += _tooltip.Hide;
		row.AddChild(slot);
	}

	private void RefreshDifficulty()
	{
		_tooltip.Hide();
		GetNode<Label>("ColorRect/TextureRect/Title").Text = _selected.DisplayName;
		int recommendedLevel = _difficulty?.RecommendedLevelOverride is > 0
			? _difficulty.RecommendedLevelOverride : _selected.RecommendedLevel;
		GetNode<Label>("ColorRect/TextureRect/Level/LevelDown").Text = recommendedLevel.ToString();
		var drops = GetNode<HBoxContainer>("ColorRect/TextureRect/ScrollContainer/FallList");
		Clear(drops);
		foreach (string id in _selected.GetPossibleDropIds(_difficulty)) AddDrop(drops, id);
		GetNode<ScrollContainer>("ColorRect/TextureRect/ScrollContainer").ScrollHorizontal = 0;
		foreach (Node child in _difficultyButtons.GetChildren())
			if (child is TextureButton button)
				button.SelfModulate = button.GetNode<Label>("Caption").Text == _difficulty?.DisplayName
					? Colors.White : new Color(0.7f, 0.7f, 0.7f);
	}

	private TextureButton CreateYellowButton(string text, Vector2 size)
	{
		var challenge = GetNode<TextureButton>("ColorRect/TextureRect/Challenge");
		var button = new TextureButton
		{
			CustomMinimumSize = size, IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TextureNormal = challenge.TextureNormal, TexturePressed = challenge.TextureHover,
			TextureHover = challenge.TextureHover
		};
		var caption = new Label { Name = "Caption", Text = text, MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		caption.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Font/HYZhuZiMeiXinTiW.ttf"));
		caption.AddThemeFontSizeOverride("font_size", 22);
		caption.AddThemeColorOverride("font_color", Colors.Yellow);
		caption.AddThemeColorOverride("font_outline_color", Colors.Black);
		caption.AddThemeConstantOverride("outline_size", 4);
		button.AddChild(caption);
		caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return button;
	}

	private static void Clear(Node node)
	{
		foreach (Node child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
	}
}
