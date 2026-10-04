using System;
using System.Collections.Generic;
using System.Globalization;
using Zaomeng.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>属性展示注册表；ID 决定替换目标，注册顺序决定分页顺序。</summary>
public sealed class CharacterStatRegistry
{
	public sealed record Entry(string Id, string Caption, Func<CharacterStats, float> Read,
		Func<float, Player, string>? Format = null);

	private readonly List<Entry> _entries = new();
	public IReadOnlyList<Entry> Entries => _entries.AsReadOnly();
	public event Action? Changed;

	public void Register(Entry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);
		ArgumentNullException.ThrowIfNull(entry.Read);
		int index = _entries.FindIndex(existing => existing.Id == entry.Id);
		if (index < 0) _entries.Add(entry);
		else _entries[index] = entry;
		Changed?.Invoke();
	}

	public bool Unregister(string id)
	{
		if (_entries.RemoveAll(entry => entry.Id == id) == 0) return false;
		Changed?.Invoke();
		return true;
	}

	public static CharacterStatRegistry CreateDefault()
	{
		var registry = new CharacterStatRegistry();
		registry.Register(new("health", "生命", s => s.MaxHealth, (v, p) => $"{Number(p.Health)}/{Number(v)}"));
		registry.Register(new("mana", "魔法", s => s.MaxMana, (v, p) => $"{Number(p.Mana)}/{Number(v)}"));
		registry.Register(new("attack", "攻击", s => s.Attack));
		registry.Register(new("luck", "幸运", s => s.Luck, Rating(50)));
		registry.Register(new("physical_defense", "物防", s => s.PhysicalDefense, Rating(250)));
		registry.Register(new("magic_defense", "魔防", s => s.MagicDefense, Rating(250)));
		registry.Register(new("critical", "暴击", s => s.CriticalRating, Rating(100)));
		registry.Register(new("dodge", "闪避", s => s.DodgeRating, Rating(100)));
		registry.Register(new("health_regen", "回血", s => s.HealthRegeneration));
		registry.Register(new("mana_regen", "回魔", s => s.ManaRegeneration));
		registry.Register(new("accuracy", "命中", s => s.Accuracy));
		registry.Register(new("toughness", "韧性", s => s.Toughness));
		registry.Register(new("life_steal", "吸血", s => s.LifeSteal, (v, _) => $"{Number(v * 100)}%"));
		registry.Register(new("armor_penetration", "破甲", s => s.ArmorPenetration));
		registry.Register(new("critical_resistance", "暴免", s => s.CriticalResistance));
		registry.Register(new("magic_penetration", "破魔", s => s.MagicPenetration));
		return registry;
	}

	public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
	private static Func<float, Player, string> Rating(float denominator) => (value, _) =>
		$"{Number(value)}({Number(MathF.Round(Math.Max(0, value) / (Math.Max(0, value) + denominator) * 100, 1))}%)";
}
