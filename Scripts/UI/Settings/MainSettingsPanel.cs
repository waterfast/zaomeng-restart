using System;
using Godot;
using Zaomeng.Settings;

namespace Zaomeng.UI;

/// <summary>绑定旧版主菜单设置；不存在的内容明确禁用。</summary>
public partial class MainSettingsPanel : Node2D
{
	private readonly System.Collections.Generic.List<(OptionButton Button, bool Level)> _musicSelectors = new();
	private const string Left = "Bg/BGColor/VBoxContainer/";
	private const string Right = "Bg/BGColor/VBoxContainer2/";
	public override void _Ready()
	{
		GetNode<Button>("Bg/Return").Pressed += QueueFree;
		BindToggle(Left + "GameMusicTitle/GMOpenOrClose", GameOption.Music);
		BindToggle(Left + "GameMusicTitle2/GM2OpenOrClose", GameOption.Effects);
		BindToggle(Left + "MonsterBloodText/MBTOpenOrClose", GameOption.MonsterHealthNumbers);
		GetNode<Label>(Left + "MonsterBloodText").Text = "Boss血量数字：";
		BindToggle(Left + "MonsterBloodShow/MBSOpenOrClose", GameOption.BossHealthBar);
		GetNode<Label>(Left + "MonsterBloodShow").Text = "Boss血条显示：";
		BindToggle(Left + "MonsterBloodShow2/MBS2OpenOrClose", GameOption.MonsterHealthBar);
		BindToggle(Left + "RoleEQ/ShowClose2", GameOption.PlayerEquipment);
		BindToggle(Right + "BodyShow/YesOrNo", GameOption.MonsterBody);
		GetNode<Label>(Right + "BodyShow").Text = "怪物身体：";
		BindToggle(Right + "HpBloodDelay/YesORNot", GameOption.SmoothHealthBars);
		BindToggle(Right + "AutomaticallyPickUpItems/YesOrNots", GameOption.AutomaticPickup);
		GetNode<Label>(Right + "AutomaticallyPickUpItems").TooltipText = "开启后，掉落物落地等待3秒自动进入背包；走近仍可提前拾取。";
		BindToggle(Right + "RoleBody/ShowClose", GameOption.PlayerBody);
		BindVolume(Left + "GameMusicFB/HSlider", true);
		BindVolume(Left + "GameMusicFB2/GM2Slider", false);
		BindMusicSelector(Right + "GameMusicTitle", false);
		BindMusicSelector(Right + "GameMusicTitle2", true);
		BindToggle(Right + "AutomaticallyPickUpItems2/Ornot", GameOption.PickupSound);
		DisableRow(Right + "LevelInfo", "当前关卡选择必须设置模式和出怪速度");
		GameSettings.Changed += Refresh;
		Refresh();
		FitViewport();
		GetViewport().SizeChanged += FitViewport;
	}

	public override void _ExitTree()
	{
		GameSettings.Changed -= Refresh;
		GetViewport().SizeChanged -= FitViewport;
	}

	private void BindMusicSelector(string path, bool level)
	{
		var row = GetNode<Label>(path);
		row.Text = level ? "关卡音乐：" : "菜单音乐：";
		var template = row.GetChild<Button>(0);
		var select = new OptionButton { Name = level ? "LevelMusic" : "MenuMusic", Position = new Vector2(115, 0), Size = new Vector2(270, 36) };
		select.AddThemeFontOverride("font", template.GetThemeFont("font"));
		select.AddThemeFontSizeOverride("font_size", 20);
		foreach (Node child in row.GetChildren()) if (child is Button button) button.Hide();
		row.AddChild(select);
		select.AddItem("跟随场景（默认）");
		var catalog = GD.Load<Zaomeng.Audio.AudioCatalog>("res://Content/Audio/Registry.tres");
		foreach (var track in catalog.Tracks) { select.AddItem(track.DisplayName); select.SetItemMetadata(select.ItemCount - 1, track.Id); }
		select.ItemSelected += index => Report(GameSettings.SetMusicSelection(level, index == 0 ? "" : select.GetItemMetadata((int)index).AsString()));
		_musicSelectors.Add((select, level));
	}
	private void BindToggle(string path, GameOption option)
	{
		var button = GetNode<Button>(path);
		button.SetMeta("option", (int)option);
		button.Pressed += () => Report(GameSettings.SetEnabled(option, !GameSettings.IsEnabled(option)));
	}

	private void BindVolume(string path, bool music)
	{
		var slider = GetNode<HSlider>(path);
		slider.SetValueNoSignal(GameSettings.Volume(music));
		slider.ValueChanged += value => Report(GameSettings.SetVolume(music, (float)value));
	}

	private void Refresh()
	{
		foreach (var (select, level) in _musicSelectors)
		{
			int selected = 0;
			for (int i = 1; i < select.ItemCount; i++) if (select.GetItemMetadata(i).AsString() == GameSettings.MusicSelection(level)) selected = i;
			select.Select(selected);
		}
		foreach (Node child in FindChildren("*", "Button", true, false))
			if (child is Button button && button.HasMeta("option"))
				button.Text = GameSettings.IsEnabled((GameOption)button.GetMeta("option").AsInt32()) ? "开启中" : "关闭中";
		GetNode<HSlider>(Left + "GameMusicFB/HSlider").SetValueNoSignal(GameSettings.Volume(true));
		GetNode<HSlider>(Left + "GameMusicFB2/GM2Slider").SetValueNoSignal(GameSettings.Volume(false));
	}

	private void DisableRow(string path, string reason)
	{
		var row = GetNode<Control>(path);
		row.TooltipText = reason;
		bool first = true;
		foreach (Node child in row.GetChildren())
			if (child is Button button)
			{
				button.Disabled = true;
				button.TooltipText = reason;
				button.Text = "未接入";
				button.Visible = first;
				first = false;
			}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
		{
			GetViewport().SetInputAsHandled();
			QueueFree();
		}
	}

	private void Report(Error result)
	{
		GetNode<Label>("Bg/BGColor/Title").Text = result == Error.Ok ? "游戏设置" : "设置保存失败";
	}

	private void FitViewport()
	{
		Vector2 size = GetViewportRect().Size;
		float scale = Mathf.Min(size.X / 940, size.Y / 590);
		Scale = Vector2.One * scale;
		Position = (size - new Vector2(940, 590) * scale) / 2;
	}
}
