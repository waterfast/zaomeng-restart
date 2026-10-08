using System;
using Godot;

namespace Zaomeng.Settings;

public enum GameOption
{
	Music, Effects, AutomaticPickup, MonsterHealthNumbers, BossHealthBar,
	MonsterHealthBar, MonsterBody, PlayerBody, PlayerEquipment, SmoothHealthBars, PickupSound
}

/// <summary>本机游戏偏好独立于角色存档，主菜单与局内设置共用。</summary>
public static class GameSettings
{
	public const string DefaultPath = "user://game_settings.cfg";
	private static ConfigFile _config = new();
	private static string _path = DefaultPath;
	private static bool _loaded;
	public static event Action? Changed;
	public static bool AutomaticPickup => IsEnabled(GameOption.AutomaticPickup);

	public static void Load(string path = DefaultPath)
	{
		_path = path;
		_config = new ConfigFile();
		Error result = _config.Load(path);
		if (result != Error.Ok && result != Error.FileNotFound)
			GD.PushWarning($"游戏设置读取失败，使用默认值：{result}");
		_loaded = true;
		ApplyAudio();
		Changed?.Invoke();
	}

	private static void EnsureLoaded() { if (!_loaded) Load(); }
	private static bool DefaultValue(GameOption option) => option is not
		(GameOption.AutomaticPickup or GameOption.SmoothHealthBars or GameOption.MonsterHealthNumbers);

	public static bool IsEnabled(GameOption option)
	{
		EnsureLoaded();
		Variant value = _config.GetValue("options", option.ToString(), DefaultValue(option));
		return value.VariantType == Variant.Type.Bool ? value.AsBool() : DefaultValue(option);
	}

	public static Error SetEnabled(GameOption option, bool enabled)
	{
		EnsureLoaded();
		_config.SetValue("options", option.ToString(), enabled);
		return Save();
	}

	public static float Volume(bool music)
	{
		EnsureLoaded();
		Variant value = _config.GetValue("audio", music ? "music_volume" : "effects_volume", 0f);
		float volume = value.VariantType is Variant.Type.Float or Variant.Type.Int ? value.AsSingle() : 0;
		return float.IsFinite(volume) ? Mathf.Clamp(volume, -80, 5) : 0;
	}

	public static Error SetVolume(bool music, float volume)
	{
		EnsureLoaded();
		_config.SetValue("audio", music ? "music_volume" : "effects_volume",
			float.IsFinite(volume) ? Mathf.Clamp(volume, -80, 5) : 0);
		return Save();
	}

	public static string MusicSelection(bool level)
	{
		EnsureLoaded();
		var value = _config.GetValue("audio", level ? "level_track" : "menu_track", "");
		return value.VariantType == Variant.Type.String ? value.AsString() : "";
	}
	public static Error SetMusicSelection(bool level, string id)
	{
		EnsureLoaded();
		_config.SetValue("audio", level ? "level_track" : "menu_track", id);
		return Save();
	}
	private static Error Save()
	{
		ApplyAudio();
		Changed?.Invoke();
		Error result = _config.Save(_path);
		if (result != Error.Ok) GD.PushWarning($"游戏设置保存失败：{result}");
		return result;
	}

	private static void ApplyAudio()
	{
		ApplyBus("Music", GameOption.Music, true);
		ApplyBus("Effects", GameOption.Effects, false);
	}

	private static void ApplyBus(string name, GameOption option, bool music)
	{
		int index = AudioServer.GetBusIndex(name);
		if (index < 0)
		{
			AudioServer.AddBus();
			index = AudioServer.BusCount - 1;
			AudioServer.SetBusName(index, name);
		}
		AudioServer.SetBusMute(index, !IsEnabled(option));
		AudioServer.SetBusVolumeDb(index, Volume(music));
	}
}
