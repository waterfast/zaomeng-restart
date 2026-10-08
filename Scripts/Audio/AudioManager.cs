using System.Collections.Generic;
using Godot;
using Zaomeng.Settings;
namespace Zaomeng.Audio;

/// <summary>常驻音乐与有限音效声道。内容通过资源配置，播放状态不属于角色存档。</summary>
public partial class AudioManager : Node
{
	public static AudioManager? Instance { get; private set; }
	public AudioCatalog Catalog { get; private set; } = null!;
	public MusicTrack? CurrentTrack { get; private set; }
	public int MusicStarts { get; private set; }
	public int EffectsPlayed { get; private set; }
	private AudioStreamPlayer _music = null!;
	private AudioStream? _musicStream;
	private readonly List<AudioStreamPlayer> _voices = new();
	private readonly Dictionary<ulong, ulong> _lastPlayed = new();
	private MusicTrack? _sceneDefault;
	private bool _inLevel;
	private bool _resultPlaying;
	private int _nextVoice;
	public const int MaximumVoices = 24;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		Catalog = GD.Load<AudioCatalog>("res://Content/Audio/Registry.tres");
		GameSettings.IsEnabled(GameOption.Music);
		_music = new AudioStreamPlayer { Name = "Music", Bus = "Music", ProcessMode = ProcessModeEnum.Always };
		AddChild(_music);
		for (int i = 0; i < MaximumVoices; i++)
		{
			var voice = new AudioStreamPlayer { Name = $"Effect{i}", Bus = "Effects", ProcessMode = ProcessModeEnum.Always };
			AddChild(voice);
			_voices.Add(voice);
		}
		GameSettings.Changed += RefreshMusic;
		// 全局绑定新界面按钮，避免各面板重复创建音效播放器。
		GetTree().NodeAdded += BindButton;
	}
	public override void _ExitTree()
	{
		StopMusic();
		foreach (var voice in _voices) { voice.Stop(); voice.Stream = null; }
		GameSettings.Changed -= RefreshMusic;
		GetTree().NodeAdded -= BindButton;
		if (Instance == this) Instance = null;
	}
	private void BindButton(Node node)
	{
		if (node is BaseButton button) button.Pressed += () => PlayEffect(Catalog.Click, ui: true);
	}
	public void EnterMenu() => Enter(false, Catalog.MenuDefault);
	public void EnterLevel(MusicTrack? track) => Enter(true, track);
	private void Enter(bool inLevel, MusicTrack? track)
	{
		foreach (var voice in _voices) voice.Stop();
		_lastPlayed.Clear();
		_resultPlaying = false;
		_inLevel = inLevel;
		_sceneDefault = track;
		RefreshMusic();
	}
	private void RefreshMusic()
	{
		if (_resultPlaying) return;
		var desired = Catalog.Find(GameSettings.MusicSelection(_inLevel)) ?? _sceneDefault;
		if (desired == CurrentTrack) return;
		CurrentTrack = desired;
		StopMusic();
		// 循环属于播放实例，避免修改资源目录中的共享音轨。
		_musicStream = desired?.Stream.Duplicate() as AudioStream;
		_music.Stream = _musicStream;
		if (_music.Stream is AudioStreamMP3 mp3) mp3.Loop = true;
		if (_music.Stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
		if (_music.Stream != null) { _music.Play(); MusicStarts++; }
	}
	private void StopMusic()
	{
		_music.Stop();
		_music.Stream = null;
		_musicStream?.Dispose();
		_musicStream = null;
	}
	public bool PlayEffect(AudioStream? sound, bool ui = false)
	{
		if (sound == null || !GameSettings.IsEnabled(GameOption.Effects) || (!ui && GetTree().Paused)) return false;
		ulong now = Time.GetTicksMsec();
		ulong id = sound.GetInstanceId();
		// 同帧的范围命中共用一声，避免多怪叠加形成尖锐爆音。
		if (_lastPlayed.TryGetValue(id, out ulong last) && now - last < 40) return false;
		_lastPlayed[id] = now;
		AudioStreamPlayer? voice = _voices.Find(v => !v.Playing);
		voice ??= _voices[_nextVoice++ % MaximumVoices];
		voice.Stop();
		voice.ProcessMode = ui ? ProcessModeEnum.Always : ProcessModeEnum.Pausable;
		voice.Stream = sound;
		voice.VolumeDb = -6;
		voice.Play();
		EffectsPlayed++;
		return true;
	}
	public void PlayPickup()
	{
		if (GameSettings.IsEnabled(GameOption.PickupSound)) PlayEffect(Catalog.Pickup);
	}
	public void PlayResult(bool victory)
	{
		_resultPlaying = true;
		foreach (var voice in _voices) voice.Stop();
		StopMusic();
		// 结果提示只播放一次，直到下一次场景入口才恢复循环音乐。
		_sceneDefault = null;
		CurrentTrack = null;
		PlayEffect(victory ? Catalog.Victory : Catalog.Defeat, ui: true);
	}
}
