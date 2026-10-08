using System;
using Godot;
using Zaomeng;
using Zaomeng.Audio;
using Zaomeng.Settings;
using Zaomeng.Level;

/// <summary>隔离本机配置验证真实音轨、播放状态、战斗调用和共用设置界面。</summary>
public partial class AudioSmokeTest : Node
{
	private const string SettingsPath = "user://audio_smoke_test.cfg";
	public override async void _Ready()
	{
		try
		{
			DirAccess.RemoveAbsolute(SettingsPath);
			GameSettings.Load(SettingsPath);
			var audio = AudioManager.Instance ?? throw new InvalidOperationException("常驻音频管理器未启动");
			foreach (var track in audio.Catalog.Tracks)
			{
				using var playback = track.Stream.InstantiatePlayback();
				Check(track.Stream.GetLength() > 0 && playback != null, $"音乐可解码：{track.Id}");
			}
			foreach (var cue in new[] { audio.Catalog.Click, audio.Catalog.Pickup, audio.Catalog.Victory, audio.Catalog.Defeat })
				Check(cue != null && cue.GetLength() > 0, "系统提示音有效");
			audio.EnterMenu();
			Check(audio.CurrentTrack?.Id == "theme1", "菜单沿用旧工程默认造梦1主题");
			int starts = audio.MusicStarts;
			audio.EnterMenu();
			GameSettings.SetVolume(true, -12);
			Check(audio.MusicStarts == starts, "相同主题与调音量不重启音乐");
			var level = GD.Load<LevelDefinition>("res://Content/Levels/forest.tres");
			audio.EnterLevel(level.Music);
			Check(audio.CurrentTrack?.Id == "1_music", "花果山默认旧版关卡音乐");
			GameSettings.SetMusicSelection(true, "theme3");
			Check(audio.CurrentTrack?.Id == "theme3", "关卡选曲立即替换");
			GameSettings.Load(SettingsPath);
			Check(GameSettings.MusicSelection(true) == "theme3", "选曲保存并重载");
			GameSettings.SetMusicSelection(true, "unknown");
			Check(audio.CurrentTrack?.Id == "1_music", "未知曲目回退场景默认");
			GameSettings.SetEnabled(GameOption.Effects, false);
			Check(!audio.PlayEffect(audio.Catalog.Click, true), "关闭音效不创建播放");
			GameSettings.SetEnabled(GameOption.Effects, true);
			GameSettings.SetEnabled(GameOption.PickupSound, false);
			int count = audio.EffectsPlayed;
			audio.PlayPickup();
			Check(audio.EffectsPlayed == count, "独立关闭拾取音效");
			Check(audio.PlayEffect(audio.Catalog.Pickup) && !audio.PlayEffect(audio.Catalog.Pickup), "同音源密集命中合并播放");
			GetTree().Paused = true;
			Check(!audio.PlayEffect(audio.Catalog.Pickup), "暂停拒绝新战斗音效");
			Check(audio.PlayEffect(audio.Catalog.Click, true), "暂停仍允许界面音效");
			GetTree().Paused = false;
			var actor = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			AddChild(actor);
			actor.InputEnabled = false;
			actor.SetPhysicsProcess(false);
			count = audio.EffectsPlayed;
			Check(actor.TryAttack() && audio.EffectsPlayed > count, "真实普攻播放对应旧版挥击声");
			count = audio.EffectsPlayed;
			var monster = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			AddChild(monster);
			monster.SetPhysicsProcess(false);
			using (var hit = new HitDefinition { CanCrit = false })
				Check(CombatResolver.Resolve(actor, monster, hit) && audio.EffectsPlayed > count, "真实命中播放打击声音");
			monster.QueueFree();
			count = audio.EffectsPlayed;
			actor.ReceiveHit(new HitResult(1, Vector2.Zero, 0.1f));
			Check(audio.EffectsPlayed > count, "角色受击播放声音");
			count = audio.EffectsPlayed;
			actor.ReceiveHit(new HitResult(10000, Vector2.Zero, 0.1f));
			Check(actor.IsDead && audio.EffectsPlayed > count, "致死播放死亡声音");
			actor.QueueFree();
			var panel = GD.Load<PackedScene>("res://Scenes/UI/Settings/GameSet.tscn").Instantiate<Node2D>();
			AddChild(panel);
			var select = panel.GetNode<OptionButton>("Bg/BGColor/VBoxContainer2/GameMusicTitle/MenuMusic");
			select.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
			audio.EnterMenu();
			Check(GameSettings.MusicSelection(false) == "theme2" && audio.CurrentTrack?.Id == "theme2", "实际设置按钮选择造梦2主题");
			if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-audio"))
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				DirAccess.MakeDirRecursiveAbsolute("res://.godot/audio-checks");
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/audio-checks/settings.png");
			}
			foreach (var track in audio.Catalog.Tracks) audio.PlayEffect(track.Stream);
			Check(audio.GetChildCount() == AudioManager.MaximumVoices + 1, "密集不同声音复用有限声道");
			audio.PlayResult(true);
			starts = audio.MusicStarts;
			GameSettings.SetVolume(true, -10);
			Check(audio.CurrentTrack == null && audio.MusicStarts == starts, "结算提示不被设置刷新恢复背景曲");
			audio.EnterMenu();
			Check(audio.CurrentTrack?.Id == "theme2", "返回菜单恢复已选背景曲");
			Check(audio.GetChildCount() == AudioManager.MaximumVoices + 1, "音效声道固定上限");
			panel.QueueFree();
			var menus = new Zaomeng.UI.MenuManager();
			AddChild(menus);
			if (!InputMap.HasAction("audio_test_pause")) InputMap.AddAction("audio_test_pause");
			var pause = GD.Load<PackedScene>("res://Scenes/UI/Settings/SetMenu.tscn").Instantiate<Control>();
			AddChild(pause);
			menus.RegisterMenu("audio_test_pause", pause);
			var binding = new Zaomeng.UI.LegacySettingsPanel();
			pause.AddChild(binding);
			binding.Bind(pause, menus, () => { });
			menus.ToggleMenu("audio_test_pause");
			pause.GetNode<Button>("bg/box/BGMControl").EmitSignal(BaseButton.SignalName.Pressed);
			var full = pause.GetNode<Node2D>("FullSettingsLayer/GameSet");
			Check(full.HasNode("Bg/BGColor/VBoxContainer2/GameMusicTitle/MenuMusic") && GetTree().Paused && !menus.IsProcessingInput(), "局内完整设置接管输入并保持暂停");
			full.GetNode<Button>("Bg/Return").EmitSignal(BaseButton.SignalName.Pressed);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(GetTree().Paused && menus.IsProcessingInput() && !pause.HasNode("FullSettingsLayer"), "关闭完整设置回到暂停菜单");
			menus.CloseMenu();
			Check(!GetTree().Paused, "继续游戏恢复运行");
			pause.QueueFree();
			menus.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			GD.Print("AudioSmokeTest: PASS");
			GameSettings.Load();
			audio.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GetTree().Paused = false;
			GameSettings.Load();
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}
	private static void Check(bool valid, string message)
	{
		if (!valid) throw new InvalidOperationException(message);
	}
}
