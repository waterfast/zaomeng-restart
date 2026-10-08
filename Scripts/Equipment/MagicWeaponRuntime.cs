using System.Collections.Generic;
using Godot;
using Zaomeng.Items;
using Zaomeng.Combat.Effects;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Equipment;

/// <summary>法宝独立施放与冷却。只消费当前装备资源，不占角色技能快捷槽。</summary>
public partial class MagicWeaponRuntime : Node
{
	private Player _player = null!;
	private SaveCharacter _character = null!;
	private ItemCatalog _catalog = null!;
	private readonly Dictionary<MagicWeaponAbility, float> _cooldowns = new();
	private MagicWeaponAbility? _casting;
	private Vector2 _origin;
	private int _direction, _released, _revision;
	private float _elapsed;
	private AttackParameters _parameters = AttackParameters.Default;
	public MagicWeaponAbility? Ability => _character.Equipment.Get(EquipmentSlot.MagicWeapon) is { } item &&
		_catalog.TryGetDefinition(item.DefinitionId, out ItemDefinition? definition) && definition is EquipmentDefinition equipment
		? equipment.MagicWeaponAbility : null;
	public float Cooldown => Ability is { } ability ? _cooldowns.GetValueOrDefault(ability) : 0;
	public void Bind(Player player, SaveCharacter character, ItemCatalog catalog)
	{ _player = player; _character = character; _catalog = catalog; }
	public bool TryUse()
	{
		if (Ability is not { } ability || _player.IsDead || !_player.Visible ||
			_player.State == ActorState.Hurt || _casting is not null || _cooldowns.GetValueOrDefault(ability) > 0) return false;
		int cost = ability.Action.GetManaCost(1);
		var parameters = _player.PassiveEffects.PrepareAttack(ability.Action);
		if (!_player.TrySpendMana(cost)) return false;
		_parameters = parameters;
		_cooldowns[ability] = ability.Action.CooldownSeconds;
		_casting = ability;
		Zaomeng.Audio.AudioManager.Instance?.PlayEffect(ability.Action.Sound);
		_origin = _player.GlobalPosition; _direction = _player.FacingDirection;
		_revision = _player.SpawnRevision; _elapsed = 0; _released = 0;
		ReleaseDue();
		return true;
	}
	public override void _PhysicsProcess(double delta)
	{
		foreach (var ability in new List<MagicWeaponAbility>(_cooldowns.Keys))
		{
			float remaining = _cooldowns[ability] - (float)delta;
			if (remaining <= 0) _cooldowns.Remove(ability); else _cooldowns[ability] = remaining;
		}
		if (_casting is not null)
		{
			if (_player.IsDead || !_player.Visible || _player.SpawnRevision != _revision || Ability != _casting) _casting = null;
			else { _elapsed += (float)delta * _parameters.AttackSpeed; ReleaseDue(); }
		}
		if (_player.InputEnabled && Input.IsActionJustPressed("artifact_skill")) TryUse();
	}
	private void ReleaseDue()
	{
		if (_casting is not { } ability) return;
		while (_released < ability.Burst.ReleaseTimes.Length && _elapsed >= ability.Burst.ReleaseTimes[_released])
			ability.Burst.Release(_player, ability.Action, 1, _released++, _origin, _direction, _parameters);
		if (_released == ability.Burst.ReleaseTimes.Length) _casting = null;
	}
}
