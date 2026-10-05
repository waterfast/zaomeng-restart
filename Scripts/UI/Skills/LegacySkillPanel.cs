using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Skills;
using Zaomeng.Character;
using Zaomeng.Items;

namespace Zaomeng.UI;

/// <summary>绑定旧学习列表和按键弹窗；所有持久状态通过学习服务写回角色。</summary>
public partial class LegacySkillPanel : Node
{
	private Node2D _root = null!;
	private SkillLearningService _skills = null!;
	private Action _save = null!;
	private Player? _player;
	private Control _list = null!;
	private Node2D _keyDialog = null!;
	private Label _message = null!;
	private string _selected = "";
	private int _rebindSlot = -1;
	private readonly List<(SkillEntry Skill, Control[] Controls, float[] BaseY)> _rows = new();
	private ItemCatalog _items = null!;
	public event Action? CloseRequested;

	public void Bind(Node2D root, SkillLearningService skills, Action save, Player? player = null)
	{
		_root = root;
		_skills = skills;
		_save = save;
		_player = player;
		_items = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
		_skills.Apply(player);
		Control front = root.GetNode<Control>("bg/lh_pic/front_bg");
		_list = GD.Load<PackedScene>("res://Scenes/UI/Skill/zd_skill.tscn").Instantiate<Control>();
		_list.Position = new(40, 50);
		_list.CustomMinimumSize = new(760, 370);
		_list.Size = new(760, 370);
		front.AddChild(_list);
		var scroll = _list.GetNode<ScrollContainer>("ScrollContainer");
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		// 原模板用绝对定位排列行，给滚动内容明确高度才能滚到最后一行。
		_list.GetNode<Control>("ScrollContainer/HBoxContainer").CustomMinimumSize = new(760, 800);
		_message = new Label { Position = new(20, 424), Size = new(790, 26), MouseFilter = Control.MouseFilterEnum.Ignore };
		_message.AddThemeFontOverride("font", GD.Load<FontFile>("res://Assets/Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf"));
		front.AddChild(_message);
		_keyDialog = GD.Load<PackedScene>("res://Scenes/UI/Skill/SkillKeySet.tscn").Instantiate<Node2D>();
		_keyDialog.Position = new(420, 220);
		root.AddChild(_keyDialog);
		_keyDialog.Hide();
		string[] keys = ["Y", "U", "I", "O", "L"];
		for (int i = 0; i < keys.Length; i++)
		{
			int slot = i;
			Button button = _keyDialog.GetNode<Button>($"BG/{keys[i]}");
			button.Pressed += () => Equip(slot);
			button.GuiInput += input =>
			{
				if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
				_rebindSlot = slot;
				_message.Text = "请按新的字母键，Esc 取消改键";
				_keyDialog.GetNode<Label>("BG/Label").Text = $"请为技能槽 {slot + 1} 按字母键";
				button.AcceptEvent();
			};
		}
		_keyDialog.GetNode<BaseButton>("Close").Pressed += CloseKeyDialog;
		root.GetNode<BaseButton>("bg/close").Pressed += () => { CloseKeyDialog(); CloseRequested?.Invoke(); };
		root.GetNode<BaseButton>("bg/zd_skill").Pressed += () => ShowTab(false);
		root.GetNode<BaseButton>("bg/bd_skill").Pressed += () => ShowTab(true);
		BuildRows();
		for (int i = 0; i < _skills.Catalog.Skills.Count; i++)
		{
			SkillEntry skill = _skills.Catalog.Skills[i];
			string row = "ScrollContainer/HBoxContainer";
			int number = i + 1;
			_list.GetNode<Label>($"{row}/Sk_na/skill_{number}").Text = skill.DisplayName;
			_list.GetNode<Label>($"{row}/sk_ms/s_{number}").Text = skill.Description;
			var icon = _list.GetNode<Button>($"{row}/sk_pi/ski_{number}");
			icon.Icon = skill.DisplayIcon;
			icon.ExpandIcon = true;
			icon.IconAlignment = HorizontalAlignment.Center;
			foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
				icon.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = Colors.Black });
			icon.Pressed += () => OpenKeyDialog(skill);
			var help = new SkillDetailsHelp { Name = "DetailsHelp", Text = "?", MouseFilter = Control.MouseFilterEnum.Stop,
				FocusMode = Control.FocusModeEnum.None };
			icon.AddChild(help);
			help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
			help.OffsetLeft = -22; help.OffsetTop = -22;
			help.AddThemeFontSizeOverride("font_size", 16);
			help.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f));
			foreach (string state in new[] { "normal", "hover", "pressed" })
				help.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.9f) });
			_list.GetNode<Button>($"{row}/sk_lv/Skill_{number}").Pressed += () => Learn(skill.Id);
		}
		ShowTab(false);
		Refresh();
		root.VisibilityChanged += OnVisibilityChanged;
	}
	private void OnVisibilityChanged() { if (_root.Visible) Refresh(); else CloseKeyDialog(); }
	public override void _ExitTree() => _root.VisibilityChanged -= OnVisibilityChanged;

	private void BuildRows()
	{
		string[] columns = ["Sk_na", "sk_pi", "sk_lv", "sk_le", "sk_ms"];
		string[] prefixes = ["skill_", "ski_", "Skill_", "sk_", "s_"];
		var rowControls = new List<Control[]>();
		var baseY = new float[columns.Length];
		for (int i = 0; i < _skills.Catalog.Skills.Count; i++) rowControls.Add(new Control[columns.Length]);
		for (int column = 0; column < columns.Length; column++)
		{
			Node parent = _list.GetNode($"ScrollContainer/HBoxContainer/{columns[column]}");
			Control template = (Control)parent.GetNode<Control>($"{prefixes[column]}1").Duplicate();
			baseY[column] = template.Position.Y;
			foreach (Node child in parent.GetChildren())
				if (child.Name.ToString().StartsWith(prefixes[column], StringComparison.Ordinal) &&
					int.TryParse(child.Name.ToString()[prefixes[column].Length..], out _))
				{ parent.RemoveChild(child); child.Free(); }
			for (int i = 0; i < rowControls.Count; i++)
			{
				var control = (Control)template.Duplicate();
				control.Name = $"{prefixes[column]}{i + 1}";
				parent.AddChild(control);
				rowControls[i][column] = control;
			}
			template.Free();
		}
		for (int i = 0; i < rowControls.Count; i++) _rows.Add((_skills.Catalog.Skills[i], rowControls[i], baseY));
	}

	public override void _Process(double delta)
	{
		if (_root.Visible) _root.GetNode<Label>("bg/lh_pic/lh_value").Text = _skills.Souls.ToString();
		else CloseKeyDialog();
	}

	public override void _Input(InputEvent input)
	{
		if (!_root.Visible || !_keyDialog.Visible || input is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode == Key.Escape) CloseKeyDialog();
		else if (_rebindSlot >= 0)
		{
			if (_skills.TrySetKey(_rebindSlot, key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode, out string message))
			{
				_rebindSlot = -1;
				Commit();
				UpdateKeyButtons();
				_keyDialog.GetNode<Label>("BG/Label").Text = "点击设置技能；右键按钮修改按键";
			}
			_message.Text = message;
		}
		GetViewport().SetInputAsHandled();
	}

	private void Learn(string id)
	{
		if (_skills.TryLearn(id, out string message)) Commit();
		_message.Text = message;
		Refresh();
	}

	private void OpenKeyDialog(SkillEntry skill)
	{
		if (skill.Passive) { _message.Text = "被动技能学会后自动生效"; return; }
		if (_skills.Level(skill.Id) == 0) { _message.Text = "请先点击学习按钮学习技能"; return; }
		_selected = skill.Id;
		_message.Text = "点击按钮装配技能；右键按钮修改按键";
		_rebindSlot = -1;
		_keyDialog.GetNode<Label>("BG/Label").Text = $"设置技能「{skill.DisplayName}」按键";
		UpdateKeyButtons();
		_keyDialog.Show();
	}

	private void UpdateKeyButtons()
	{
		string[] keys = ["Y", "U", "I", "O", "L"];
		for (int i = 0; i < keys.Length; i++)
		{
			var button = _keyDialog.GetNode<Button>($"BG/{keys[i]}");
			button.Text = $"设置为按键 {_skills.KeyName(i)}";
			button.TooltipText = "点击装配技能；右键修改这个技能槽的按键";
		}
	}

	private void Equip(int slot)
	{
		if (_skills.TryEquip(slot, _selected, out string message))
		{
			Commit();
			CloseKeyDialog();
		}
		_message.Text = message;
		Refresh();
	}

	private void Commit() { _skills.Apply(_player); _save(); }
	private void CloseKeyDialog() { _keyDialog.Hide(); _rebindSlot = -1; }

	private void Refresh()
	{
		CharacterStats stats = CharacterStatCalculator.Calculate(_skills.Character, _items);
		float bonus = stats.SkillLevelBonus;
		for (int i = 0; i < _skills.Catalog.Skills.Count; i++)
		{
			SkillEntry skill = _skills.Catalog.Skills[i];
			int level = _skills.Level(skill.Id);
			string row = "ScrollContainer/HBoxContainer";
			int effective = SkillLevelResolver.Effective(level, bonus, skill.MaximumLevel);
			_list.GetNode<Label>($"{row}/sk_le/sk_{i + 1}").Text = effective == level ? $"Lv:{level}" : $"Lv:{level}\n生效:{effective}";
			var button = _list.GetNode<Button>($"{row}/sk_lv/Skill_{i + 1}");
			button.Icon = null;
			button.Text = level == 0 ? "学习" : "升级";
			_list.GetNode<Label>($"{row}/sk_ms/s_{i + 1}").Text = skill.Description;
			bool max = level >= skill.MaximumLevel;
			button.Disabled = max;
			if (max) button.Text = "满级";
			button.GetNode<Label>("Need").Text = max ? "已满级" : $"需：{skill.LearningCost(level)}灵魂";
			button.TooltipText = "";
			_list.GetNode<Button>($"{row}/sk_pi/ski_{i + 1}/DetailsHelp").TooltipText =
				SkillDetailsDescription.Describe(skill, effective, stats.HasteRating);
		}
		_root.GetNode<Label>("bg/lh_pic/lh_value").Text = _skills.Souls.ToString();
	}

	private void ShowTab(bool passive)
	{
		CloseKeyDialog();
		Refresh();
		_root.GetNode<Label>("bg/lh_pic/front_bg/Title").Text = passive ? "被动技能" : "主动技能";
		int visibleRow = 0;
		foreach (var (skill, controls, baseY) in _rows)
		{
			bool visible = skill.Passive == passive;
			for (int column = 0; column < controls.Length; column++)
			{
				Control control = controls[column];
				control.Visible = visible;
				control.Position = new(control.Position.X, baseY[column] + visibleRow * 110);
			}
			if (visible) visibleRow++;
		}
		_list.GetNode<Control>("ScrollContainer/HBoxContainer").CustomMinimumSize = new(760, Math.Max(160, visibleRow * 110 + 65));
		_list.GetNode<ScrollContainer>("ScrollContainer").ScrollVertical = 0;
	}
}
