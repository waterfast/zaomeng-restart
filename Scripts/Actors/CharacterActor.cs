using Godot;

namespace Zaomeng;
//这是角色动作基类，控制角色动作
public enum ActorState { Free, Attacking, Hurt, Dead }//角色状态

public partial class CharacterActor : CharacterBody2D
{
	[Export] public int Team { get; set; }//队伍
	[Export] public float MaxHealth { get; set; } = 100;//最大体力
	[Export] public float MoveSpeed { get; set; } = 220;//移动速度
	[Export] public float Gravity { get; set; } = 1000;//重力
	[Export] public float KnockbackFriction { get; set; } = 350;//击退摩擦力
	// 每个角色只配置这一份普攻列表；一段攻击也放一个 AttackStep。
	[Export] public Godot.Collections.Array<AttackStep> NormalCombo { get; set; } = new();
	[Export] public float ComboGracePeriod { get; set; } = 0.22f;
	[Export] public StringName IdleAnimation { get; set; } = "wait";//待机动画
	[Export] public StringName MoveAnimation { get; set; } = "run";//移动动画
	[Export] public StringName AirAnimation { get; set; } = "jump1";//空中动画

	public float Health { get; private set; }
	public ActorState State { get; private set; }
	public bool IsDead => State == ActorState.Dead;
	public int FacingDirection { get; private set; } = 1;
	public CharacterMotor Motor { get; } = new();
	public AnimationPlayer Animator { get; private set; } = null!;
	public HitBox AttackBox { get; private set; } = null!;
	public HitDefinition? CurrentHit { get; private set; }
	public int CurrentComboStage { get; private set; } = -1;
	protected float MoveDirection;//移动方向
	private Node2D _facing = null!;
	private ProgressBar _healthBar = null!;
	private float _hurtRemaining;
	private float _comboGraceRemaining;
	private int _nextComboStage;
	private bool _queuedNextAttack;
	private StringName _currentAttackAnimation = "";
	private AttackStep? _currentAttackStep;
	private SkillCast? _skillCast;
	private ActionMotionPlayback? _actionMotion;

	public override void _Ready()
	{
		Health = MaxHealth;
		_facing = GetNode<Node2D>("Facing");
		Animator = GetNode<AnimationPlayer>("AnimationPlayer");
		AttackBox = GetNode<HitBox>("Facing/HitBox");
		_healthBar = GetNode<ProgressBar>("HealthBar");
		_healthBar.MaxValue = MaxHealth;
		_healthBar.Value = Health;
		Animator.AnimationFinished += OnAnimationFinished;
		Face(1);
		Play(IdleAnimation);
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;
		MoveDirection = ReadMovementAxis();
		UpdateHurtState(step);//更新受击状态
		UpdateComboWindow(step);//更新连段普攻
		UpdateSkillCast();
		if (State == ActorState.Free) ReadIntent(step);//Free时读取
		if (State == ActorState.Free) UpdateFreeMovementVisuals();//Free时更新移动动画

		float moveSpeed = GetHorizontalSpeed();
		Motor.Step(this, moveSpeed, Gravity, KnockbackFriction, step);
	}

	private float GetHorizontalSpeed()
	{
		if (State == ActorState.Free) return MoveDirection * MoveSpeed;
		// 受击击退由 Motor 单独处理，不能再叠加动作冲刺。
		if (State != ActorState.Attacking || !Mathf.IsZeroApprox(Motor.ExternalVelocityX)) return 0;
		return _actionMotion?.GetHorizontalSpeed() ?? 0;
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
			_skillCast.Stop();
			_skillCast = null;
			_actionMotion = null;
			CurrentHit = null;
			if (State == ActorState.Attacking) State = ActorState.Free;
			return;
		}
		_skillCast.Update();
		// 即使动画轨道也改了 HitBox.Active，窗口外仍不能造成伤害。
		CurrentHit = _skillCast.IsHitActive ? _skillCast.Definition.Hit : null;
	}

	private void UpdateFreeMovementVisuals()
	{
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

		State = ActorState.Attacking;
		CurrentComboStage = stage;
		CurrentHit = attack.Hit;
		_currentAttackStep = attack;
		_currentAttackAnimation = attack.Animation;
		_queuedNextAttack = false;
		_comboGraceRemaining = 0;
		AttackBox.BeginAttack();
		StartMotion(motion, attack.Animation, attack.FramesPerSecond);
		Play(attack.Animation);
		return true;
	}

	public bool TryUseSkill(SkillDefinition? skill)
	{
		if (State != ActorState.Free || skill == null) return false;
		if (!IsSkillConfigured(skill)) return false;
		ActionMotion? motion = SelectMotion(skill.Motion, skill.AirMotion);

		// 技能开始时打断普攻连段；它和普攻共用角色的命中框。
		ResetCombo();
		State = ActorState.Attacking;
		CurrentHit = null;
		AttackBox.BeginAttack();
		_skillCast = new SkillCast(skill, Animator, AttackBox, _facing, GetParent());
		StartMotion(motion, skill.Animation, skill.FramesPerSecond);
		Play(skill.Animation);
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
		if (skill.Hit != null && (skill.HitStartFrame < 0
			|| skill.HitEndFrame <= skill.HitStartFrame
			|| skill.HitStartFrame / (double)skill.FramesPerSecond >= animationLength))
		{
			// 美术可以先调动画；攻击帧尚未配置时，播放技能但不开放命中框。
			GD.PushWarning($"{Name}: 技能攻击框的开始帧或结束帧无效，本次只播放动画，不产生攻击命中");
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
		if (State != ActorState.Attacking || _currentAttackStep?.EffectScene is not { } scene) return;
		ActorEffectSpawner.SpawnAttached(scene, _facing,
			_currentAttackStep.EffectOffset, _currentAttackStep.EffectLifetime);
	}

	public void ReceiveHit(HitResult hit)
	{
		if (IsDead) return;
		Health = Mathf.Max(0, Health - hit.Damage);
		_healthBar.Value = Health;
		AttackBox.Active = false;
		_skillCast?.Stop();
		_skillCast = null;
		_actionMotion = null;
		ResetCombo();
		Motor.ApplyKnockback(this, hit.Knockback);
		State = Health <= 0 ? ActorState.Dead : ActorState.Hurt;
		_hurtRemaining = hit.Hitstun;
		Play(IsDead ? "death" : "hurt", restart: true);
	}

	private void OnAnimationFinished(StringName animation)//动作结束
	{
		if (_skillCast != null && animation == _skillCast.Definition.Animation)
		{
			_skillCast.Stop();
			_skillCast = null;
			_actionMotion = null;
			CurrentHit = null;
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
		CurrentHit = null;
		CurrentComboStage = -1;
		_queuedNextAttack = false;
		_currentAttackStep = null;
		_currentAttackAnimation = "";
		_nextComboStage = 0;
		_comboGraceRemaining = 0;
	}

	protected void Play(StringName animation, bool restart = false)
	{
		if (!restart && Animator.CurrentAnimation == animation && Animator.IsPlaying()) return;
		Animator.Play(animation);
		// Apply frame-zero reset tracks immediately when an attack is interrupted.
		Animator.Advance(0);
	}

}
