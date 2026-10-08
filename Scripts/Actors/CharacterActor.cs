using Godot;

namespace Zaomeng;
//这是角色动作基类，控制角色动作
public enum ActorState { Free, Attacking, Hurt, Dead }//角色状态

public partial class CharacterActor : CharacterBody2D
{
	[Export] public Zaomeng.Audio.ActorSoundProfile? SoundProfile { get; set; }
	[Export] public int Team { get; set; }//队伍
	[Export] public float MaxHealth { get; set; } = 100;//最大体力
	[Export] public float Attack { get; set; } = 12;
	[Export] public int Level { get; set; } = 1;
	[Export] public float PhysicalDefense { get; set; }
	[Export] public float MagicDefense { get; set; }
	[Export] public float DefenseConstant { get; set; } = 250;
	[Export] public float CriticalRating { get; set; }
	[Export] public float CriticalResistance { get; set; }
	[Export] public float DodgeRating { get; set; }
	[Export] public float Accuracy { get; set; }
	[Export] public float Toughness { get; set; }
	[Export] public float ArmorPenetration { get; set; }
	[Export] public float MagicPenetration { get; set; }
	[Export] public float LifeSteal { get; set; }
	[Export] public float Luck { get; set; }
	[Export] public float CriticalLuckConstant { get; set; } = 50;
	[Export] public float MoveSpeed { get; set; } = 220;//移动速度
	[Export] public float Gravity { get; set; } = 1000;//重力
	[Export] public float KnockbackFriction { get; set; } = 350;//击退摩擦力
	// 每个角色只配置这一份普攻列表；一段攻击也放一个 AttackStep。
	[Export] public Godot.Collections.Array<AttackStep> NormalCombo { get; set; } = new();
	[Export] public float ComboGracePeriod { get; set; } = 0.22f;
	[Export] public StringName IdleAnimation { get; set; } = "wait";//待机动画
	[Export] public StringName MoveAnimation { get; set; } = "run";//移动动画
	[Export] public StringName AirAnimation { get; set; } = "jump1";//空中动画
	[Export] public StringName HurtAnimation { get; set; } = "hurt";

	public float Health { get; private set; }
	protected void RestoreEntranceHealth(float health) => Health = Mathf.Clamp(health, 1, MaxHealth);
	public ActorState State { get; private set; }
	public bool IsDead => State == ActorState.Dead;
	public int SpawnRevision { get; private set; }
	public event System.Action<HitResult>? HitReceived;
	public event System.Action<float>? HealingReceived;
	public event System.Action? AttackDodged;
	internal void ReportDodge() => AttackDodged?.Invoke();
	public bool IsInvulnerable => State == ActorState.Attacking && _skillCast?.Definition.Invulnerable == true;
	public int FacingDirection { get; private set; } = 1;
	public CharacterMotor Motor { get; } = new();
	public AnimationPlayer Animator { get; private set; } = null!;
	public HitBox AttackBox { get; private set; } = null!;
	public HitDefinition? CurrentHit { get; private set; }
	public int CurrentHitLevel { get; private set; } = 1;
	public SkillDefinition? CurrentSkill => _skillCast?.Definition;
	public int CurrentComboStage { get; private set; } = -1;
	protected float MoveDirection;//移动方向
	private Node2D _facing = null!;
	private float _hurtRemaining;
	private float _comboGraceRemaining;
	private int _nextComboStage;
	private bool _queuedNextAttack;
	private StringName _currentAttackAnimation = "";
	private AttackStep? _currentAttackStep;
	private SkillCast? _skillCast;
	private Zaomeng.Skills.SkillBehavior? _skillBehavior;
	private ActionMotionPlayback? _actionMotion;

	/// <summary>最大生命变更只截断超出的血量，穿脱装备不会治疗或复活角色。</summary>
	protected void RefreshHealthLimit()
	{
		Health = Mathf.Clamp(Health, 0, Mathf.Max(0, MaxHealth));
	}

	public override void _Ready()
	{
		Health = MaxHealth;
		AddToGroup("combat_actors");
		// 沿斜坡吸附地面，避免下坡浮空；坡度上限按迁入关卡的真实地形设置。
		FloorSnapLength = 8;
		FloorMaxAngle = Mathf.DegToRad(55);
		_facing = GetNode<Node2D>("Facing");
		Animator = GetNode<AnimationPlayer>("AnimationPlayer");
		AttackBox = GetNode<HitBox>("Facing/HitBox");
		InitializePassiveEffects();
		Animator.AnimationFinished += OnAnimationFinished;
		Face(1);
		Play(IdleAnimation);
		AddChild(new Zaomeng.Settings.ActorPresentationSettings { Name = "PresentationSettings" });
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;
		Buffs.Tick(step);
		MoveDirection = Buffs.PreventsActions ? 0 : ReadMovementAxis();
		UpdateHurtState(step);//更新受击状态
		UpdateComboWindow(step);//更新连段普攻
		UpdateSkillCast();
		if (State == ActorState.Free && !Buffs.PreventsActions) ReadIntent(step);//Free时读取
		if (State == ActorState.Free) UpdateFreeMovementVisuals();//Free时更新移动动画

		float moveSpeed = GetHorizontalSpeed();
		Motor.Step(this, moveSpeed * Buffs.MoveSpeedMultiplier, Gravity, KnockbackFriction, step);
	}

	private float GetHorizontalSpeed()
	{
		if (State == ActorState.Free) return MoveDirection * MoveSpeed;
		// 受击击退由 Motor 单独处理，不能再叠加动作冲刺。
		if (State != ActorState.Attacking || !Mathf.IsZeroApprox(Motor.ExternalVelocityX)) return 0;
		return (_actionMotion?.GetHorizontalSpeed() ?? 0) * CurrentAttackParameters.MotionSpeed;
	}

	private void UpdateHurtState(float delta)
	{
		if (State != ActorState.Hurt) return;
		_hurtRemaining -= delta;
		if (_hurtRemaining <= 0) State = ActorState.Free;
	}

	private void UpdateComboWindow(float delta)
	{
		if (State != ActorState.Free || _comboGraceRemaining <= 0) return;
		_comboGraceRemaining -= delta;
		if (_comboGraceRemaining <= 0) _nextComboStage = 0;
	}

	private void UpdateSkillCast()
	{
		if (_skillCast == null) return;
		if (Animator.CurrentAnimation != _skillCast.Definition.Animation)
		{
			// 外部动画也可能打断技能；不能把旧技能的位移或攻击框留在角色身上。
			CancelSkillBehavior();
			_skillCast.Stop();
			_skillCast = null;
			ResetAttackParameters();
			_actionMotion = null;
			CurrentHit = null;
			CurrentHitLevel = 1;
			if (State == ActorState.Attacking) State = ActorState.Free;
			return;
		}
		_skillCast.Update();
		// 即使动画轨道也改了 HitBox.Active，窗口外仍不能造成伤害。
		CurrentHit = _skillCast.ActiveHit;
	}

	private void UpdateFreeMovementVisuals()
	{
		if (!Buffs.ForcedAnimation.IsEmpty) { Play(Buffs.ForcedAnimation); return; }
		if (!Mathf.IsZeroApprox(MoveDirection)) Face(Mathf.Sign(MoveDirection));

		if (!IsOnFloor()) Play(SelectAirAnimation());
		else if (Mathf.IsZeroApprox(MoveDirection)) Play(IdleAnimation);
		else Play(MoveAnimation);
	}

	protected virtual StringName SelectAirAnimation() => AirAnimation;

	// 移动轴每帧采样；能否用于移动仍由当前动作决定。
	protected virtual float ReadMovementAxis() => 0;
	protected virtual void ReadIntent(float delta) { }

	public void Face(int direction)
	{
		if (direction == 0) return;
		FacingDirection = direction > 0 ? 1 : -1;
		// Original sheets face left; mirror visual and attack shape together.
		_facing.Scale = new(-FacingDirection, 1);
	}

	public bool TryAttack()
	{
		if (Buffs.PreventsActions) return false;
		if (State == ActorState.Attacking)
		{
			if (_skillCast != null || CurrentComboStage + 1 >= NormalCombo.Count) return false;
			// 攻击时只缓存一次按键，当前段结束时才接下一段。
			_queuedNextAttack = true;
			return true;
		}
		if (State != ActorState.Free || NormalCombo.Count == 0) return false;
		int stage = _comboGraceRemaining > 0 && _nextComboStage < NormalCombo.Count ? _nextComboStage : 0;
		return StartAttack(stage);
	}

	private bool StartAttack(int stage)
	{
		if (stage < 0 || stage >= NormalCombo.Count) return false;
		AttackStep? attack = NormalCombo[stage];
		if (attack == null || attack.Hit == null || !Animator.HasAnimation(attack.Animation))
		{
			GD.PushWarning($"{Name}: 普攻第 {stage + 1} 段缺少 AttackStep、命中数据或动画");
			return false;
		}
		ActionMotion? motion = SelectMotion(attack.Motion, attack.AirMotion);
		if (!IsMotionConfigured(motion, attack.Animation, attack.FramesPerSecond)) return false;
		PrepareAttackParameters(null);

		State = ActorState.Attacking;
		CurrentComboStage = stage;
		CurrentHit = attack.Hit;
		CurrentHitLevel = 1;
		_currentAttackStep = attack;
		_currentAttackAnimation = attack.Animation;
		_queuedNextAttack = false;
		_comboGraceRemaining = 0;
		AttackBox.BeginAttack();
		StartMotion(motion, attack.Animation, attack.FramesPerSecond);
		Play(attack.Animation);
		Zaomeng.Audio.AudioManager.Instance?.PlayEffect(attack.Sound);
		return true;
	}

	public virtual bool TryUseSkill(SkillDefinition? skill)
	{
		if (State != ActorState.Free || skill == null || Buffs.PreventsActions) return false;
		if (!IsSkillConfigured(skill)) return false;
		ActionMotion? motion = SelectMotion(skill.Motion, skill.AirMotion);

		// 技能开始时打断普攻连段；它和普攻共用角色的命中框。
		ResetCombo();
		PrepareAttackParameters(skill);
		State = ActorState.Attacking;
		Zaomeng.Audio.AudioManager.Instance?.PlayEffect(skill.Sound);
		CurrentHit = null;
		CurrentHitLevel = GetSkillLevel(skill);
		AttackBox.BeginAttack();
		_skillCast = new SkillCast(skill, Animator, AttackBox, _facing, GetParent(), CurrentAttackParameters);
		StartMotion(motion, skill.Animation, skill.FramesPerSecond);
		Play(skill.Animation);
		if (skill.BehaviorScene is { } behaviorScene)
		{
			CancelSkillBehavior();
			_skillBehavior = behaviorScene.Instantiate<Zaomeng.Skills.SkillBehavior>();
			AddChild(_skillBehavior);
			_skillBehavior.Begin(this, skill);
		}
		UpdateSkillCast(); // 第 0 帧也可以释放特效或开启攻击框。
		return true;
	}

	private bool IsSkillConfigured(SkillDefinition skill)
	{
		if (skill.FramesPerSecond <= 0 || !Animator.HasAnimation(skill.Animation))
		{
			GD.PushWarning($"{Name}: 技能缺少动画或帧率无效");
			return false;
		}

		double animationLength = Animator.GetAnimation(skill.Animation).Length;
		int previousEnd = 0;
		foreach (HitEvent? hitEvent in skill.Hits)
		{
			if (hitEvent?.Hit == null || hitEvent.StartFrame < previousEnd
				|| hitEvent.EndFrame <= hitEvent.StartFrame
				|| hitEvent.StartFrame / (double)skill.FramesPerSecond >= animationLength)
			{
				GD.PushWarning($"{Name}: {skill.Animation} 的技能命中窗口无效");
				return false;
			}
			previousEnd = hitEvent.EndFrame;
		}
		if (skill.EffectScene != null && (skill.EffectFrame < 0
			|| skill.EffectFrame / (double)skill.FramesPerSecond >= animationLength))
		{
			GD.PushWarning($"{Name}: 技能特效帧超出动画时长");
			return false;
		}
		if (!IsMotionConfigured(SelectMotion(skill.Motion, skill.AirMotion),
			skill.Animation, skill.FramesPerSecond)) return false;
		return true;
	}

	protected virtual int GetSkillLevel(SkillDefinition skill) => 1;

	private ActionMotion? SelectMotion(ActionMotion? groundMotion, ActionMotion? airMotion)
		=> !IsOnFloor() && airMotion != null ? airMotion : groundMotion;

	private bool IsMotionConfigured(ActionMotion? motion, StringName animation, int framesPerSecond)
	{
		if (motion == null) return true;
		if (motion.IsValidFor(Animator.GetAnimation(animation).Length, framesPerSecond)) return true;
		GD.PushWarning($"{Name}: {animation} 的动作位移配置无效");
		return false;
	}

	private void StartMotion(ActionMotion? motion, StringName animation, int framesPerSecond)
	{
		// 只继承角色自身速度，不把尚未衰减完的受击击退算进动作惯性。
		float entryVelocityX = Velocity.X - Motor.ExternalVelocityX;
		_actionMotion = motion == null ? null : new ActionMotionPlayback(
			motion, Animator, animation, framesPerSecond, entryVelocityX, FacingDirection);
	}

	// 在 AnimationPlayer 的方法轨道上调用；特效随 Facing 一起转向。
	public void SpawnAttackEffect()
	{
		if (State != ActorState.Attacking || _currentAttackStep is not { } attack) return;
		var scene = CurrentAttackParameters.EffectScene ?? attack.EffectScene;
		if (scene is null) return;
		ActorEffectSpawner.SpawnAttached(scene, _facing,
			attack.EffectOffset, attack.EffectLifetime, CurrentAttackParameters);
	}

	public void CancelSkillBehavior()
	{
		if (IsInstanceValid(_skillBehavior)) _skillBehavior!.Stop();
		_skillBehavior = null;
	}
	public virtual void StartPendingSkillCooldown(SkillDefinition skill) { }
	public override void _ExitTree() => CancelSkillBehavior();

	public HitResult? ReceiveHit(HitResult hit)
	{
		if (IsDead || IsInvulnerable) return null;
		hit = hit with { Damage = hit.Damage * Buffs.IncomingMultiplier };
		if (hit.Damage <= 0) return null;
		CombatTextSpawner.ShowDamage(this, hit);
		Health = Mathf.Max(0, Health - hit.Damage);
		Zaomeng.Audio.AudioManager.Instance?.PlayEffect(Health <= 0 ? SoundProfile?.Death : SoundProfile?.Hurt);
		HitReceived?.Invoke(hit);
		// 霸体只阻止打断与击退；致死伤害必须继续走完整死亡清理。
		if (Health > 0 && Buffs.SuperArmor) return hit;
		AttackBox.Active = false;
		CancelSkillBehavior();
		_skillCast?.Stop();
		_skillCast = null;
		_actionMotion = null;
		ResetCombo();
		Motor.ApplyKnockback(this, hit.Knockback);
		State = Health <= 0 ? ActorState.Dead : ActorState.Hurt;
		if (IsDead) Buffs.Clear();
		_hurtRemaining = hit.Hitstun;
		Play(IsDead ? "death" : HurtAnimation, restart: true);
		return hit;
	}

	public void Heal(float amount)
	{
		if (IsDead || amount <= 0) return;
		float recovered = Mathf.Min(MaxHealth - Health, amount);
		Health = Mathf.Min(MaxHealth, Health + amount);
		if (recovered > 0) HealingReceived?.Invoke(recovered);
	}

	/// <summary>对象池再次启用角色时清理上一次战斗的瞬时状态。</summary>
	public virtual void ResetForSpawn(Vector2 position)
	{
		// 池复用后仍是同一个节点，旧弹体与跟踪特效必须识别这已经是新的一次出生。
		SpawnRevision++;
		Buffs.Clear();
		PassiveEffects.RefreshBuffSources();
		CancelSkillBehavior();
		_skillCast?.Stop();
		_skillCast = null;
		_actionMotion = null;
		ResetCombo();
		AttackBox.Active = false;
		Motor.ApplyKnockback(this, Vector2.Zero);
		Velocity = Vector2.Zero;
		GlobalPosition = position;
		Health = MaxHealth;
		State = ActorState.Free;
		_hurtRemaining = 0;
		Face(-1);
		Play(IdleAnimation, restart: true);
	}

	private void OnAnimationFinished(StringName animation)//动作结束
	{
		if (_skillCast != null && animation == _skillCast.Definition.Animation)
		{
			if (IsInstanceValid(_skillBehavior)) _skillBehavior!.Complete(animation);
			CancelSkillBehavior();
			_skillCast.Stop();
			_skillCast = null;
			ResetAttackParameters();
			_actionMotion = null;
			CurrentHit = null;
			CurrentHitLevel = 1;
			State = ActorState.Free;
			return;
		}
		if (State == ActorState.Attacking && CurrentComboStage >= 0
			&& animation == _currentAttackAnimation)
		{
			AttackBox.Active = false;
			int nextStage = CurrentComboStage + 1;
			if (_queuedNextAttack && nextStage < NormalCombo.Count && StartAttack(nextStage))
			{
				return;
			}
			State = ActorState.Free;
			ResetAttackParameters();
			_actionMotion = null;
			CurrentHit = null;
			CurrentComboStage = -1;
			_queuedNextAttack = false;
			_currentAttackStep = null;
			_currentAttackAnimation = "";
			// 动画结束后留一小段补按时间；超时则从第一段重新开始。
			_nextComboStage = nextStage < NormalCombo.Count ? nextStage : 0;
			_comboGraceRemaining = nextStage < NormalCombo.Count ? ComboGracePeriod : 0;
		}
	}

	private void ResetCombo()
	{
		ResetAttackParameters();
		CurrentHit = null;
		CurrentHitLevel = 1;
		CurrentComboStage = -1;
		_queuedNextAttack = false;
		_currentAttackStep = null;
		_currentAttackAnimation = "";
		_nextComboStage = 0;
		_comboGraceRemaining = 0;
	}

	protected void Play(StringName animation, bool restart = false)
	{
		Animator.SpeedScale = State == ActorState.Attacking ? CurrentAttackParameters.AttackSpeed : 1;
		if (!restart && Animator.CurrentAnimation == animation && Animator.IsPlaying()) return;
		Animator.Play(animation);
		// Apply frame-zero reset tracks immediately when an attack is interrupted.
		Animator.Advance(0);
	}

}
