using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Skills;

namespace Zaomeng;

/// <summary>不访问存档，逐技能检查原版特效、左右镜像、目标爆炸及回收清理。</summary>
public partial class HeroEffectsSmokeTest : Node2D
{
	private Player? _player;
	private Monster _target = null!;
	private bool _capture;
	public override void _PhysicsProcess(double delta) => _player?._PhysicsProcess(delta);
	public override async void _Ready()
	{
		try
		{
			_capture = OS.GetCmdlineUserArgs().Contains("--capture-effects");
			RenderingServer.SetDefaultClearColor(new Color("273344"));
			SkillCatalogRegistry.Default.Validate();
			_target = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			_target.MaxHealth = 1000000;
			_target.AiEnabled = false;
			AddChild(_target);
			_target.SetPhysicsProcess(false);
			foreach (string role in new[] { "role_1", "role_2" })
			{
				if (OS.GetCmdlineUserArgs().Contains("--tangseng-only") && role != "role_2") continue;
				if (OS.GetCmdlineUserArgs().Contains("--explosion-only") && role != "role_1") continue;
				var catalog = SkillCatalogRegistry.Default.Get(role);
				_player = catalog.ActorScene.Instantiate<Player>();
				_player.MaxMana = 10000;
				_player.InputEnabled = false;
				_player.Gravity = 0;
				AddChild(_player);
				_player.SetPhysicsProcess(false);
				foreach (var entry in catalog.Skills.Where(entry => entry.Action is not null && !OS.GetCmdlineUserArgs().Contains("--explosion-only")))
				{
					var skill = entry.Action!;
					foreach (int direction in new[] { 1, -1 })
					{
						float sourceX = entry.Id == "smb" ? (direction > 0 ? 220 : 720) : 470;
						_player.ResetForSpawn(new(sourceX, 420));
						_player.SetPhysicsProcess(false);
						_player.Face(direction);
						_player.RestoreMana(10000);
						_target.ResetForSpawn(new(sourceX + direction * (entry.Id == "smb" ? 520 : 100), 420));
						Check(_player.TryUseSkill(skill), $"{role}/{entry.Id}/{direction} starts");
						var label = new Label { Text = $"{catalog.DisplayName} · {entry.DisplayName} · {(direction > 0 ? "向右" : "向左")}", Position = new(20,20), ZIndex = 100 };
						AddChild(label);
						float sample = entry.Id switch { "hyjj" => .82f, "sgq" => .9f, "xbz" => .78f, "jhsj" => .7f, "smb" => 1.65f, "blb" => .65f, "tjgl" => .85f, "hmz" => .2f, "zz" => .58f, "myhc" or "jgz" => .35f, _ => .15f };
						await Wait(sample);
						if (role == "role_1")
						{
							var visual = _player.GetNode<AnimatedSprite2D>("Facing/Visual/SpecialEffect");
							if (entry.Id is "zz" or "lys" or "lyfb")
								Check(visual.Visible && HasPixels(visual), $"{entry.Id} visible original effect");
							if (entry.Id == "hyjj")
							{
								var fire = GetChildren().OfType<FireEyesEffect>().Single();
								Check(fire.GetChildren().OfType<AnimatedSprite2D>().Any(HasPixels), "enemy explosion has pixels");
								_target.Position += new Vector2(30,0);
								await Wait(.04);
								Check(fire.GlobalPosition.IsEqualApprox(_target.GlobalPosition), "fire eyes follows enemy");
							}
						}
						else if (entry.Id is "xbz" or "jhsj" or "shy" or "smb" or "sgq" or "myhc" or "jgz")
						{
							var effects = GetChildren().OfType<AnimatedSkillEffect>().ToArray();
							Check(effects.Length > 0 && effects.Any(HasVisibleArt), $"{entry.Id} original visual clip released");
							if (entry.Id == "sgq") Check(GetChildren().OfType<SkillProjectile>().Any(), "holy ball emits radial light blades");
							if (entry.Id == "smb") Check(_target.Health < _target.MaxHealth, "water explosion reaches enemy beneath falling water");
						}
						if (_capture) await Capture($"{role}-{entry.Id}-{direction}");
						label.QueueFree();
						_player.ResetForSpawn(new(470,420));
						await Wait(.06);
						Check(!GetChildren().OfType<AnimatedSkillEffect>().Any() && !GetChildren().OfType<FireEyesEffect>().Any(), "reset clears released effects");
						await Wait(Math.Max(0, skill.CooldownSeconds - sample) + .12);
					}
				}
				if (role == "role_1") await CheckExplosionRounds();
				if (role == "role_2") await CheckNormalDirections();
				_player.QueueFree();
				_player = null;
				await Wait(.05);
			}
			// 等待最后一段出招/命中声音结束，避免退出时仍持有原生播放实例。
			await Wait(3);
			GD.Print("HERO EFFECTS SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private async Task CheckExplosionRounds()
	{
		_player!.ResetForSpawn(new(470,420));
		_player.SetPhysicsProcess(false);
		_target.ResetForSpawn(new(570,420));
		int pulses = 0;
		void CountPulse(HitResult hit) => pulses++;
		_target.HitReceived += CountPulse;
		var fire = GD.Load<PackedScene>("res://Content/Skills/Behaviors/FireEyesImpact.tscn").Instantiate<FireEyesEffect>();
		fire.Configure(_player, _target, sourceSkill: GD.Load<SkillDefinition>("res://Content/Skills/Wukong/hyjj.tres"));
		AddChild(fire);
		await Wait(3.75);
		_target.HitReceived -= CountPulse;
		Check(pulses == 9 && !GetChildren().OfType<FireEyesEffect>().Any(), "three enemy explosion rounds each hit three times and expire");
	}
	private async Task CheckNormalDirections()
	{
		foreach (int direction in new[] { 1, -1 })
		{
			_player!.ResetForSpawn(new(470,420));
			_player.SetPhysicsProcess(false);
			_player.Face(direction);
			Check(_player.TryAttack(), "normal attack starts");
			await Wait(.28);
			var projectile = GetChildren().OfType<SkillProjectile>().Single();
			Check(Mathf.IsZeroApprox(projectile.Rotation) && projectile.Scale.IsEqualApprox(Vector2.One), "detached projectile clears inherited rotation and scale");
			var visual = projectile.GetChildren().OfType<AnimatedSprite2D>().Single();
			Check(Mathf.IsZeroApprox(visual.Rotation) && Math.Sign(visual.Scale.X) == -direction, "normal projectile faces horizontal travel");
			Vector2 first = projectile.GlobalPosition;
			await Wait(.1);
			Check((projectile.GlobalPosition.X - first.X) * direction > 0 && Mathf.IsEqualApprox(projectile.GlobalPosition.Y, first.Y), "normal projectile travels horizontally in facing direction");
			if (_capture) await Capture($"role_2-normal-{direction}");
			_player.ResetForSpawn(new(470,420));
			await Wait(.1);
		}
	}
	private static bool HasPixels(AnimatedSprite2D sprite) => sprite.SpriteFrames.GetFrameTexture(sprite.Animation, sprite.Frame) is { } texture && texture.GetImage().GetUsedRect().Size != Vector2I.Zero;
	private static bool HasVisibleArt(Node node)
	{
		if (node is AnimatedSprite2D sprite && sprite.IsVisibleInTree() && HasPixels(sprite)) return true;
		if (node is Sprite2D { Texture: not null } body && body.IsVisibleInTree()) return true;
		return node.GetChildren().Any(HasVisibleArt);
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task Capture(string name)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://.godot/hero-effects"));
		GetViewport().GetTexture().GetImage().SavePng($"res://.godot/hero-effects/{name}.png");
	}
	private static void Check(bool value, string description)
	{
		if (!value) throw new InvalidOperationException(description);
		GD.Print("PASS: " + description);
	}
}
