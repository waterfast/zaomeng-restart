using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Level;
using Zaomeng.Monsters;
using Zaomeng.Skills;

namespace Zaomeng;

/// <summary>平地上的真实施法巡检。仅使用测试场景显式登记的内容，不接入存档。</summary>
public partial class BossAnimationShowcase : Node2D
{
	[Export] public Godot.Collections.Array<MonsterDefinition> Bosses { get; set; } = new();
	private readonly List<(MonsterDefinition Boss, MonsterSkillChoice Choice, int Direction)> _actions = new();
	private Player _target = null!;
	private MonsterPool _pool = null!;
	private Monster? _boss;
	private Label _title = null!;
	private Label _status = null!;
	private int _requestedAction;
	private int _revision;
	private int _hitCount;
	private bool _verify;
	private bool _capture;
	private bool _rage;

	public override async void _Ready()
	{
		try
		{
			_verify = OS.GetCmdlineUserArgs().Contains("--verify");
			_capture = OS.GetCmdlineUserArgs().Contains("--capture-boss");
			_rage = OS.GetCmdlineUserArgs().Contains("--rage-boss");
			Check(Bosses.Count > 0, "演示目录为空");
			foreach (string file in DirAccess.GetFilesAt("res://Content/Monsters/Templates"))
				if (file.EndsWith(".tres", StringComparison.Ordinal) &&
					GD.Load<Resource>($"res://Content/Monsters/Templates/{file}") is MonsterDefinition { IsBoss: true } registered)
					Check(Bosses.Any(b => b.Id == registered.Id), $"演示遗漏已登记的 Boss：{registered.DisplayName}");
			foreach (var definition in Bosses)
			{
				definition.Validate();
				Check(definition.IsBoss, $"{definition.Id} 不是 Boss");
				foreach (int direction in new[] { 1, -1 })
					// 普攻先展示，技能顺序仍来自模板；不在运行时根据 Boss ID 分支。
					foreach (var choice in definition.Skills.OrderBy(c => c.Skill.Animation == "hit1" ? 0 : 1))
						_actions.Add((definition, choice, direction));
			}
			BuildArena();
			while (IsInsideTree())
			{
				int index = _requestedAction;
				int revision = _revision;
				var action = _actions[index];
				_boss = _pool.Spawn(action.Boss, new(350, 478));
				_boss.AiEnabled = false;
				_boss.Face(action.Direction);
				// 多段技能用近处木桩验证全部窗口，其他动作按模板的可施放距离选点。
				float distance = action.Choice.Skill.Hits.Count > 1 ? 55 : Mathf.Min(110, action.Choice.MaximumRange);
				_target.ResetForSpawn(new(350 + action.Direction * distance, 478));
				_target.Face(-action.Direction);
				_hitCount = 0;
				_title.Text = $"{index + 1}/{_actions.Count}　{action.Boss.DisplayName}　{ActionName(action.Choice.Skill)}　{(action.Direction > 0 ? "朝右" : "朝左")}";
				await Frames(20);
				if (revision == _revision)
				{
					Check(_boss.IsOnFloor() && _target.IsOnFloor(), "角色没有站在平地上");
					if (_rage)
					{
						var conditions = action.Boss.PassiveSkills.Select(s => s.Effect)
							.OfType<Zaomeng.Combat.Effects.SkillParameterEffect>()
							.Select(e => e.Condition).OfType<Zaomeng.Combat.Effects.HealthRatioCondition>().ToArray();
						if (conditions.Length > 0)
						{
							float ratio = conditions.Min(c => c.BelowRatio) * 0.9f;
							_boss.ReceiveHit(new HitResult(_boss.MaxHealth * (1 - ratio), Vector2.Zero, 0, DamageType.True));
							// 设置巡检血量产生的飘字不属于招式表现，避免遮住蓄力球。
							foreach (var text in _boss.GetParent().GetChildren().OfType<LegacyDamageText>()) text.QueueFree();
							await Frames(2);
							_title.Text += "　低血量强化";
						}
					}
					Check(_boss.TryUseSkill(action.Choice.Skill), $"{action.Choice.Skill.Id} 无法施放");
					CheckWindCharge(action.Choice.Skill);
					Check(_boss.GetNode<Node2D>("Facing").Scale.X == -action.Direction, "左右镜像错误");
					await PlayAction(action.Choice.Skill, revision);
					if (revision == _revision)
					{
						Check(_hitCount > 0, $"{action.Choice.Skill.Id} 未实际命中");
						Check(_target.Health < _target.MaxHealth, $"{action.Choice.Skill.Id} 命中但没有造成伤害");
						if (action.Choice.Skill.Hits.Count > 1)
							Check(_hitCount == action.Choice.Skill.Hits.Count, $"连续震击只命中 {_hitCount}/{action.Choice.Skill.Hits.Count} 段");
						if (action.Choice.Skill.BehaviorScene is { } scene)
						{
							var behavior = scene.Instantiate();
							try
							{
								if (behavior is AreaAttackBehavior area)
									Check(_hitCount == area.Hits.Count, $"地面爆发只命中 {_hitCount}/{area.Hits.Count} 段");
								if (behavior is ProjectileSkillBehavior projectile)
									Check(_hitCount == projectile.ReleaseTimes.Length, $"弹体只命中 {_hitCount}/{projectile.ReleaseTimes.Length} 次");
							}
							finally { behavior.Free(); }
						}
						Check(_boss.State == ActorState.Free && !_boss.AttackBox.Active, "动作结束后攻击框残留");
						CheckEffectsHidden(_boss);
						Check(!_boss.GetParent().GetChildren().Any(n => n is AreaAttackEffect or SkillProjectile), "世界特效播放结束后未回收");
						GD.Print($"PASS BOSS {action.Boss.Id}/{action.Choice.Skill.Id}/{action.Direction}: {_hitCount} hits");
						if (_verify) await VerifyInterruption(action.Choice.Skill, action.Direction);
						await Frames(35);
					}
				}
				_pool.Release(action.Boss, _boss);
				_boss = null;
				if (revision != _revision) continue;
				_requestedAction = (index + 1) % _actions.Count;
				if (_verify && _requestedAction == 0)
				{
					GD.Print($"BOSS ANIMATION SHOWCASE PASSED: {_actions.Count} actions");
					GetTree().Quit();
					return;
				}
			}
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			if (_status is not null) _status.Text = $"测试失败：{error.Message}";
			if (_verify) GetTree().Quit(1);
		}
	}

	private void BuildArena()
	{
		RenderingServer.SetDefaultClearColor(new Color("18242f"));
		var floor = new StaticBody2D { Position = new(470, 505), CollisionLayer = 1, CollisionMask = 0 };
		floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new(2200, 50) } });
		AddChild(floor);
		AddChild(new Polygon2D { Color = new("344b53"), Polygon = [new(-800, 480), new(1800, 480), new(1800, 650), new(-800, 650)] });
		AddChild(new Line2D { Points = [new(-800, 480), new(1800, 480)], Width = 2, DefaultColor = new("a3b8aa") });
		_target = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
		_target.Name = "Player";
		_target.MaxHealth = 100000;
		AddChild(_target);
		_target.InputEnabled = false;
		_target.HitReceived += _ =>
		{
			_hitCount++;
		};
		var enemies = new Node2D { Name = "Enemies", Position = new(120, 0) };
		AddChild(enemies);
		_pool = new MonsterPool(enemies);
		var hud = new CanvasLayer { ProcessMode = ProcessModeEnum.Always };
		AddChild(hud);
		var panel = new VBoxContainer { Position = new(20, 18), Size = new(900, 140) };
		hud.AddChild(panel);
		_title = new Label();
		_title.AddThemeFontSizeOverride("font_size", 24);
		panel.AddChild(_title);
		_status = new Label();
		panel.AddChild(_status);
		panel.AddChild(new Label { Text = "真实平地 · 固定受击木桩 · 自动循环全部动作 · 不使用存档" });
		var controls = new HBoxContainer();
		panel.AddChild(controls);
		AddButton(controls, "上一项", () => SelectAction(-1));
		AddButton(controls, "重播", () => SelectAction(0));
		AddButton(controls, "下一项", () => SelectAction(1));
		AddButton(controls, "普通 / 低血量", () => { _rage = !_rage; SelectAction(0); });
		AddButton(controls, "暂停 / 继续", () => GetTree().Paused = !GetTree().Paused);
		var speed = new OptionButton();
		foreach (string label in new[] { "0.25 倍", "0.5 倍", "1 倍" }) speed.AddItem(label);
		speed.Select(2);
		speed.ItemSelected += index => Engine.TimeScale = new[] { 0.25, 0.5, 1.0 }[index];
		controls.AddChild(speed);
	}

	private async Task PlayAction(SkillDefinition skill, int revision)
	{
		double length = _boss!.Animator.GetAnimation(skill.Animation).Length;
		double duration = length / _boss.CurrentAttackParameters.AttackSpeed;
		double observation = duration + 1.8;
		if (skill.BehaviorScene is { } scene)
		{
			var behavior = scene.Instantiate();
			try
			{
				if (behavior is GroundAttackSequenceBehavior sequence)
				{
					double effectDuration = Enumerable.Range(0, sequence.Frames.GetFrameCount(sequence.Animation))
						.Sum(i => sequence.Frames.GetFrameDuration(sequence.Animation, i)) / sequence.Frames.GetAnimationSpeed(sequence.Animation);
					observation = Math.Max(observation, duration + sequence.ReleaseTimes.Max() + effectDuration + .2);
				}
			}
			finally { behavior.Free(); }
		}
		int frameCount = (int)Math.Ceiling(observation * 60);
		bool captured = false;
		bool effectObserved = false;
		int capturePhase = 0;
		for (int i = 0; i < frameCount && revision == _revision; i++)
		{
			await Frames(1);
			CheckWindCharge(skill);
			string state = _boss.State == ActorState.Attacking ? "出招" : "待机";
			_status.Text = $"动作时间：{_boss.Animator.CurrentAnimationPosition:0.00} 秒　命中：{_hitCount} 次　状态：{state}";
			var visual = _boss.GetNode<Node2D>("Facing/Visual");
			foreach (var child in VisualItems(visual))
				if (child.Visible) effectObserved = true;
			foreach (var effect in _boss.GetParent().GetChildren().OfType<AreaAttackEffect>())
				Check(Mathf.Abs(effect.GlobalPosition.Y - 480) < 1, "地面爆发脚点偏离平地");
			if (!captured && _hitCount > 0)
			{
				captured = true;
				await Capture($"{_requestedAction + 1:00}-{skill.Id}");
			}
			if (_capture && capturePhase < 3 && i / 60.0 >= new[] { 0, duration * 0.65, duration + 0.4 }[capturePhase])
			{
				await Capture($"{_requestedAction + 1:00}-{skill.Id}-phase{capturePhase + 1}");
				capturePhase++;
			}
		}
		var animation = _boss.Animator.GetAnimation(skill.Animation);
		bool showsCast = false;
		for (int track = 0; track < animation.GetTrackCount(); track++)
			if (animation.TrackGetPath(track).ToString() == "Facing/Visual/Cast:visible")
				for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
					showsCast |= animation.TrackGetKeyValue(track, key).AsBool();
		if (revision == _revision && (skill.Hits.Count > 1 || showsCast))
			Check(effectObserved || skill.EffectScene is not null, $"{skill.Id} 缺少随身特效");
	}

	private async Task VerifyInterruption(SkillDefinition skill, int direction)
	{
		_boss!.ResetForSpawn(new(350, 478));
		_boss.Face(direction);
		_target.ResetForSpawn(new(350 + direction * 110, 478));
		await Frames(3);
		Check(_boss.TryUseSkill(skill), "打断检查无法施放");
		await Frames(2);
		_boss.ReceiveHit(new HitResult(1, Vector2.Zero, 0.1f, DamageType.True));
		CheckEffectsHidden(_boss);
		int hits = _hitCount;
		await Frames(100);
		Check(_hitCount == hits && !_boss.AttackBox.Active, "中断后补发伤害");
		_boss.ResetForSpawn(new(350, 478));
		CheckEffectsHidden(_boss);
		Check(_boss.TryUseSkill(skill), "死亡清理检查无法施放");
		await Frames(2);
		_boss.ReceiveHit(new HitResult(100000, Vector2.Zero, 0, DamageType.True));
		Check(_boss.IsDead, "死亡检查未进入死亡状态");
		CheckEffectsHidden(_boss);
		_boss.ResetForSpawn(new(350, 478));
		CheckEffectsHidden(_boss);
	}

	private void CheckWindCharge(SkillDefinition skill)
	{
		// 此处针对迁入内容核对表现，通用施法代码不认识 Boss 或技能 ID。
		if (skill.Id != "macaque_wind" || _boss!.State != ActorState.Attacking) return;
		double time = _boss.Animator.CurrentAnimationPosition;
		var charge = _boss.GetNode<Sprite2D>("Facing/Visual/WindCharge/Modifier/Visual");
		// 轨道时间以 Float32 保存；加速播放恰好跨键时按相同精度比较。
		bool charging = time < (double)0.7f || (time >= (double)0.9f && time < (double)1.4f);
		Check(charge.Visible == charging, $"手上蓝球在 {time:0.000} 秒的可见状态错误");
		if (charging)
			Check(charge.Texture is not null && charge.Frame is >= 1 and <= 3 && charge.Scale.X >= 0.85f,
				$"手上蓝球在 {time:0.000} 秒缺帧或缩放异常");
	}

	private static IEnumerable<CanvasItem> VisualItems(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child.Name == "Body") continue;
			if (child is Sprite2D or AnimatedSprite2D) yield return (CanvasItem)child;
			foreach (var item in VisualItems(child)) yield return item;
		}
	}

	private static void CheckEffectsHidden(Monster boss)
	{
		foreach (var child in VisualItems(boss.GetNode("Facing/Visual")))
			Check(!child.Visible, $"{child.Name} 光效未清理");
	}

	private async Task Capture(string name)
	{
		if (!_capture) return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		DirAccess.MakeDirRecursiveAbsolute("res://.godot/boss-checks");
		GetViewport().GetTexture().GetImage().SavePng($"res://.godot/boss-checks/{(_rage ? "rage-" : "")}{name}.png");
	}

	private void SelectAction(int offset)
	{
		_requestedAction = (_requestedAction + offset + _actions.Count) % _actions.Count;
		_revision++;
		GetTree().Paused = false;
	}

	private static string ActionName(SkillDefinition skill) => string.IsNullOrWhiteSpace(skill.DisplayName) ? skill.Animation.ToString() : skill.DisplayName;
	private static void AddButton(Node parent, string text, Action action)
	{
		var button = new Button { Text = text };
		button.Pressed += action;
		parent.AddChild(button);
	}
	private async Task Frames(int count)
	{
		double remaining = count / 60.0;
		while (remaining > 0)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!GetTree().Paused) remaining -= GetPhysicsProcessDeltaTime();
		}
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_target is null) return;
		// ReceiveHit 在通知后才设置击退，因此每个物理帧清零，不能只在通知里清零。
		_target.Motor.ApplyKnockback(_target, Vector2.Zero);
		_target.Velocity = Vector2.Zero;
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
	public override void _ExitTree() => Engine.TimeScale = 1;
}
