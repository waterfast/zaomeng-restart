using Godot;
using System;
using System.Collections.Generic;
using Zaomeng.Equipment;
using SaveCharacter = Zaomeng.Character.Character;
using Zaomeng.Character;
using Zaomeng.Items;
using Zaomeng.Skills;
using Zaomeng.Equipment.Skills;

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
	private SaveCharacter? _characterData;
	private ItemCatalog? _itemCatalog;
	public CharacterStats? CalculatedStats { get; private set; }
	private readonly EquipmentSkillRuntime _equipmentSkills = new();
	public IReadOnlyList<EquipmentSkillDefinition> GrantedEquipmentSkills => _equipmentSkills.Skills;
	public event Action<HitResult>? DamageDealt;
	internal float ModifyOutgoingDamage(CharacterActor target, SkillDefinition? sourceSkill, float damage)
		=> _equipmentSkills.ModifyOutgoingDamage(this, target, sourceSkill, damage);
	internal void NotifyHitDealt(CharacterActor target, HitResult hit)
	{
		DamageDealt?.Invoke(hit);
		_equipmentSkills.OnHitDealt(this, target, hit);
	}
	public override void ResetForSpawn(Vector2 position)
	{
		base.ResetForSpawn(position);
		GetNode<CanvasItem>("Facing/Visual/RoleBody").Show();
		GetNode<CanvasItem>("Facing/Visual/RoleEquipment").Show();
		if (GetNodeOrNull<AnimatedSprite2D>("Facing/Visual/Death") is { } death) { death.Stop(); death.Hide(); }
	}
	[Export] public float MaxMana { get; set; } = 50;
	public float Mana { get; private set; }
	public string CharacterId => _characterData?.Id ?? "role_1";
	public long Experience => _characterData?.Experience ?? 0;
	public bool IsMaximumLevel => Level >= CharacterProgression.MaxLevel;
	public long ExperienceToNextLevel => CharacterProgression.ExperienceToNextLevel(Level);
	public event Action? ProgressionChanged;
	public event Action<int>? SoulsCollected;
	public event Action? CombatHitConfirmed;
	internal void ConfirmCombatHit() => CombatHitConfirmed?.Invoke();
	private readonly System.Collections.Generic.Dictionary<SkillDefinition, CooldownState> _skillCooldowns = new();
	private readonly record struct CooldownState(float Remaining, float Duration, bool Pending = false);
	private float _recoveryClock;
	private SkillBehavior? _skillBehavior;

	public override void _Ready()
	{
		base._Ready();
		Mana = MaxMana;
	}

	public void RestoreMana(float amount)
	{
		if (IsDead || amount <= 0 || !float.IsFinite(amount)) return;
		Mana = Mathf.Min(MaxMana, Mana + amount);
	}

	public bool TrySpendMana(float amount)
	{
		if (IsDead || amount < 0 || !float.IsFinite(amount) || Mana < amount) return false;
		Mana -= amount;
		return true;
	}

	public float GetSkillCooldown(SkillDefinition skill)
		=> _skillCooldowns.TryGetValue(skill, out CooldownState state) ? state.Remaining : 0;

	public float GetSkillCooldownDuration(SkillDefinition skill) => _skillCooldowns.TryGetValue(skill, out CooldownState state) && (state.Remaining > 0 || state.Pending)
		? state.Duration : SkillCooldownCalculator.Calculate(skill.CooldownSeconds, CalculatedStats?.HasteRating ?? 0);

	public int EffectiveSkillLevel(string id)
	{
		var entry = SkillCatalogRegistry.Default.Find(CharacterId)?.Find(id);
		return SkillLevelResolver.Effective(GetSkillLevelFromSave(id), CalculatedStats?.SkillLevelBonus ?? 0, entry?.MaximumLevel ?? 1);
	}

	public override bool TryUseSkill(SkillDefinition? skill)
	{
		if (skill is null || (_skillCooldowns.TryGetValue(skill, out var cooldown) && (cooldown.Pending || cooldown.Remaining > 0))) return false;
		if (_characterData is not null && (GetSkillLevelFromSave(skill.Id) < 1 ||
			SkillCatalogRegistry.Default.Get(CharacterId).Find(skill.Id)?.Action != skill)) return false;
		int manaCost = Math.Max(0, skill.GetManaCost(GetSkillLevel(skill)));
		if (Mana < manaCost || !base.TryUseSkill(skill)) return false;
		// 动作校验成功后才扣蓝、快照冷却，失败按键不消耗资源。
		TrySpendMana(manaCost);
		float duration = SkillCooldownCalculator.Calculate(skill.CooldownSeconds, CalculatedStats?.HasteRating ?? 0);
		_skillCooldowns[skill] = new(skill.StartCooldownOnImpact ? 0 : duration, duration, skill.StartCooldownOnImpact);
		if (skill.LinkedCooldownSkill is { } linked)
		{
			float linkedDuration = SkillCooldownCalculator.Calculate(skill.LinkedCooldownSeconds, CalculatedStats?.HasteRating ?? 0);
			_skillCooldowns[linked] = new(linkedDuration, linkedDuration);
		}
		if (IsInstanceValid(_skillBehavior)) _skillBehavior!.Stop();
		_skillBehavior = null;
		if (skill.BehaviorScene is { } behaviorScene)
		{
			_skillBehavior = behaviorScene.Instantiate<SkillBehavior>();
			AddChild(_skillBehavior);
			_skillBehavior.Begin(this, skill);
		}
		return true;
	}
	public void StartPendingSkillCooldown(SkillDefinition skill)
	{
		if (_skillCooldowns.TryGetValue(skill, out var state) && state.Pending)
			_skillCooldowns[skill] = new(state.Duration, state.Duration);
	}
	private int GetSkillLevelFromSave(string id) => _characterData?.SkillLevels.GetValueOrDefault(id) ?? 0;

	public void GainExperience(long amount)
	{
		if (_characterData is null || amount <= 0 || IsDead || IsMaximumLevel) return;
		_characterData.Experience += Math.Min(amount, long.MaxValue - _characterData.Experience);
		if (_characterData.Experience >= ExperienceToNextLevel)
		{
			// 沿用旧版：升级清空本级经验，不把超出门槛的经验结转到下一等级。
			_characterData.Level++;
			_characterData.Experience = 0;
			CharacterProgression.SyncBaseStats(_characterData);
			RefreshCharacterStats();
			Heal(MaxHealth);
			RestoreMana(MaxMana);
			CombatTextSpawner.ShowLevelUp(this);
		}
		ProgressionChanged?.Invoke();
	}

	public bool TryCollectSouls(int amount)
	{
		if (IsDead || amount <= 0) return false;
		// 灵魂是存档共用货币，交给关卡写入 Wallet，不混入角色属性。
		SoulsCollected?.Invoke(amount);
		return true;
	}

	public void BindCharacter(SaveCharacter character, ItemCatalog catalog)
	{
		_characterData = character;
		_itemCatalog = catalog;
		RefreshCharacterStats();
	}

	public void RefreshCharacterStats()
	{
		if (_characterData is null || _itemCatalog is null) return;
		CharacterStats stats = CharacterStatCalculator.Calculate(_characterData, _itemCatalog);
		_equipmentSkills.Refresh(_characterData, _itemCatalog);
		CalculatedStats = stats;
		Level = _characterData.Level;
		MaxHealth = stats.MaxHealth;
		MaxMana = Mathf.Max(0, stats.MaxMana);
		Mana = Mathf.Clamp(Mana, 0, MaxMana);
		Attack = stats.Attack;
		PhysicalDefense = stats.PhysicalDefense;
		MagicDefense = stats.MagicDefense;
		CriticalRating = stats.CriticalRating;
		CriticalResistance = stats.CriticalResistance;
		DodgeRating = stats.DodgeRating;
		Accuracy = stats.Accuracy;
		Luck = stats.Luck;
		Toughness = stats.Toughness;
		ArmorPenetration = stats.ArmorPenetration;
		MagicPenetration = stats.MagicPenetration;
		LifeSteal = stats.LifeSteal;
		if (IsNodeReady()) RefreshHealthLimit();
	}

	protected override int GetSkillLevel(SkillDefinition skill) => _characterData is null ? 1 : EffectiveSkillLevel(skill.Id);

	public override void _PhysicsProcess(double delta)
	{
		UpdateResources((float)delta);
		if (IsOnFloor()) _jumpsUsed = 0;
		if (InputEnabled) ReadCombatButtons();
		bool wasOnFloor = IsOnFloor();
		base._PhysicsProcess(delta);
		// 走出平台时也视为消耗地面跳，避免空中额外跳两次。
		if (wasOnFloor && !IsOnFloor() && _jumpsUsed == 0) _jumpsUsed = 1;
	}

	private void UpdateResources(float delta)
	{
		foreach (SkillDefinition skill in new System.Collections.Generic.List<SkillDefinition>(_skillCooldowns.Keys))
		{
			if (_skillCooldowns[skill].Pending) continue;
			float remaining = _skillCooldowns[skill].Remaining - delta;
			if (remaining <= 0) _skillCooldowns.Remove(skill);
			else _skillCooldowns[skill] = _skillCooldowns[skill] with { Remaining = remaining };
		}
		if (IsDead) return;
		_recoveryClock += delta;
		// 旧版每秒恢复一次，基础回魔为零；装备和永久加成通过汇总属性提供。
		while (_recoveryClock >= 1)
		{
			_recoveryClock -= 1;
			RestoreMana(CalculatedStats?.ManaRegeneration ?? 0);
			Heal(Mathf.Floor(CalculatedStats?.HealthRegeneration ?? 0));
		}
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

	public SkillDefinition? GetEquippedSkill(int slot) => slot switch
	{
		0 => EquippedSkill1,
		1 => EquippedSkill2,
		2 => EquippedSkill3,
		3 => EquippedSkill4,
		4 => EquippedSkill5,
		_ => null
	};

	public void SetEquippedSkill(int slot, SkillDefinition? skill)
	{
		switch (slot)
		{
			case 0: EquippedSkill1 = skill; break;
			case 1: EquippedSkill2 = skill; break;
			case 2: EquippedSkill3 = skill; break;
			case 3: EquippedSkill4 = skill; break;
			case 4: EquippedSkill5 = skill; break;
			default: throw new ArgumentOutOfRangeException(nameof(slot));
		}
	}

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
