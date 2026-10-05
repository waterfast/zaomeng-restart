using System;
using Godot;
using Zaomeng.Save;

namespace Zaomeng.UI;

public static class GameAudioSettings
{
	private const string Path = "user://audio_settings.cfg";
	private static readonly ConfigFile Config = new();
	public static bool MusicEnabled { get; private set; } = true;
	public static bool EffectsEnabled { get; private set; } = true;
	public static void Load()
	{
		Config.Load(Path);
		MusicEnabled = Config.GetValue("audio", "music", true).AsBool();
		EffectsEnabled = Config.GetValue("audio", "effects", true).AsBool();
		Apply();
	}
	public static void Toggle(bool music)
	{
		if (music) MusicEnabled = !MusicEnabled; else EffectsEnabled = !EffectsEnabled;
		Config.SetValue("audio", "music", MusicEnabled);
		Config.SetValue("audio", "effects", EffectsEnabled);
		Config.Save(Path);
		Apply();
	}
	private static void Apply()
	{
		int music = AudioServer.GetBusIndex("Music");
		int effects = AudioServer.GetBusIndex("Effects");
		if (music >= 0) AudioServer.SetBusMute(music, !MusicEnabled);
		if (effects >= 0) AudioServer.SetBusMute(effects, !EffectsEnabled);
	}
}

/// <summary>旧局内设置的继续、保存返回和音乐按钮。</summary>
public partial class LegacySettingsPanel : Node
{
	public void Bind(Control root, MenuManager menus, Action save)
	{
		GameAudioSettings.Load();
		root.GetNode<BaseButton>("bg/close").Pressed += menus.CloseMenu;
		root.GetNode<BaseButton>("bg/box/continue_game").Pressed += menus.CloseMenu;
		root.GetNode<BaseButton>("bg/box/continue_game2").Pressed += () => Return(GameSession.FirstMap);
		root.GetNode<BaseButton>("bg/box/continue_game4").Pressed += () => Return("res://Scenes/UI/MainMenu/MainMenu.tscn");
		var music = root.GetNode<Button>("bg/box/BGMControl");
		var effects = root.GetNode<Button>("bg/box/RoleOrMonsterControl");
		void Refresh() { music.Text = GameAudioSettings.MusicEnabled ? "关闭音乐" : "打开音乐"; effects.Text = GameAudioSettings.EffectsEnabled ? "关闭音效" : "打开音效"; }
		music.Pressed += () => { GameAudioSettings.Toggle(true); Refresh(); };
		effects.Pressed += () => { GameAudioSettings.Toggle(false); Refresh(); };
		Refresh();
		void Return(string scene)
		{
			save();
			menus.CloseMenu();
			GetTree().ChangeSceneToFile(scene);
		}
	}
}
