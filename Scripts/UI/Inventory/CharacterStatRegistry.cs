using System;
using System.Collections.Generic;
using System.Globalization;
using Zaomeng.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>属性展示注册表；ID 决定替换目标，注册顺序决定分页顺序。</summary>
public sealed class CharacterStatRegistry
{
	public sealed record Entry(string Id, string Caption, Func<CharacterStats, float> Read,
		Func<float, Player, string>? Format = null, string Description = "");

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

	public static CharacterStatRegistry CreateDefault() =>
		CharacterStatCatalog.Default.CreateRegistry();

	public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
