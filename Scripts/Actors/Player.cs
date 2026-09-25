using Godot;

namespace Zaomeng;

public partial class Player : CharacterActor
{
	private static readonly StringName[] SkillActions =
	{
		"skill_1", "skill_2", "skill_3", "skill_4", "skill_5"
	};

	[Export] public float JumpSpeed { get; set; } = 430;
	[Export] public int MaxJumps { get; set; } = 2;
	[Export] public StringName DoubleJumpAnimation { get; set; } = "jump2";
	[Export] public PackedScene? DoubleJumpEffect { get; set; }
	[Export] public Vector2 DoubleJumpEffectOffset { get; set; } = new(0, -45);
	[Export] public float DoubleJumpEffectLifetime { get; set; } = 0.5f;
	// 这五项是当前装备栏，不是固定技能；同一个技能可换到不同快捷键位置。
	[ExportGroup("Equipped Skills")]
	[Export] public SkillDefinition? EquippedSkill1 { get; set; }
	[Export] public SkillDefinition? EquippedSkill2 { get; set; }
	[Export] public SkillDefinition? EquippedSkill3 { get; set; }
	[Export] public SkillDefinition? EquippedSkill4 { get; set; }
	[Export] public SkillDefinition? EquippedSkill5 { get; set; }
	[ExportGroup("")]
	public bool InputEnabled { get; set; } = true;
	private int _jumpsUsed;

	public override void _PhysicsProcess(double delta)
	{
		if (IsOnFloor()) _jumpsUsed = 0;
		if (InputEnabled) ReadCombatButtons();
		bool wasOnFloor = IsOnFloor();
		base._PhysicsProcess(delta);
		// 走出平台时也视为消耗地面跳，避免空中额外跳两次。
		if (wasOnFloor && !IsOnFloor() && _jumpsUsed == 0) _jumpsUsed = 1;
	}

	private void ReadCombatButtons()
	{
		// 读取动作名而非物理按键；以后重绑按键不用改角色代码。
		for (int i = 0; i < SkillActions.Length; i++)
		{
			if (!Input.IsActionJustPressed(SkillActions[i])) continue;
			TryUseSkill(GetEquippedSkill(i));
			break;
		}
		// 攻击中仍要读 J，才能缓存下一段普攻。
		if (Input.IsActionJustPressed("attack")) TryAttack();
	}

	private SkillDefinition? GetEquippedSkill(int slot) => slot switch
	{
		0 => EquippedSkill1,
		1 => EquippedSkill2,
		2 => EquippedSkill3,
		3 => EquippedSkill4,
		4 => EquippedSkill5,
		_ => null
	};

	protected override float ReadMovementAxis()
		=> InputEnabled ? Input.GetAxis("move_left", "move_right") : 0;

	protected override void ReadIntent(float delta)
	{
		if (!InputEnabled) return;
		if (!Mathf.IsZeroApprox(MoveDirection)) Face(Mathf.Sign(MoveDirection));
		if (Input.IsActionJustPressed("jump")) TryJump();
	}

	protected override StringName SelectAirAnimation()
	{
		// Play the second-jump clip once, then return to the regular airborne pose.
		return _jumpsUsed >= 2 && Animator.CurrentAnimation == DoubleJumpAnimation && Animator.IsPlaying()
			? DoubleJumpAnimation : AirAnimation;
	}

	public bool TryJump()
	{
		if (IsOnFloor()) _jumpsUsed = 0;
		if (State != ActorState.Free || _jumpsUsed >= MaxJumps) return false;
		bool isDoubleJump = _jumpsUsed == 1 && !IsOnFloor();
		Velocity = new(Velocity.X, -JumpSpeed);
		_jumpsUsed++;
		if (isDoubleJump)
		{
			if (Animator.HasAnimation(DoubleJumpAnimation)) Play(DoubleJumpAnimation, restart: true);
			if (DoubleJumpEffect is { } effect)
				ActorEffectSpawner.SpawnInWorld(effect, GetParent(),
					ToGlobal(DoubleJumpEffectOffset), DoubleJumpEffectLifetime);
		}
		return true;
	}
}
