using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace Zaomeng.Monsters;
[GlobalClass]
public partial class MonsterDropTable : Resource
{
	[Export] public Godot.Collections.Array<MonsterDropEntry> Entries { get; set; } = new();
	[Export] public float RollProbability { get; set; } = 1;
	[Export] public bool ChooseOne { get; set; }
	public IEnumerable<string> PossibleItemIds => RollProbability > 0
		? Entries.Where(entry => entry.Probability > 0).Select(entry => entry.ItemId)
		: Enumerable.Empty<string>();
	public void Validate()
	{
		if (!float.IsFinite(RollProbability) || RollProbability is < 0 or > 1)
			throw new InvalidOperationException("掉落总概率无效。");
		foreach (var entry in Entries)
			if (entry is null || string.IsNullOrWhiteSpace(entry.ItemId) || !float.IsFinite(entry.Probability)
				|| entry.Probability is < 0 or > 1 || entry.MinimumCount < 1 || entry.MaximumCount < entry.MinimumCount)
				throw new InvalidOperationException("怪物掉落表配置无效。");
	}
	public IEnumerable<(string ItemId, int Count)> Roll(RandomNumberGenerator random)
	{
		if (random.Randf() >= RollProbability) yield break;
		if (ChooseOne)
		{
			float total = 0;
			foreach (var entry in Entries) total += entry.Probability;
			float pick = random.Randf() * total;
			foreach (var entry in Entries)
			{
				pick -= entry.Probability;
				if (pick >= 0) continue;
				yield return (entry.ItemId, random.RandiRange(entry.MinimumCount, entry.MaximumCount));
				yield break;
			}
			yield break;
		}
		foreach (var entry in Entries)
			if (random.Randf() < entry.Probability)
				yield return (entry.ItemId, random.RandiRange(entry.MinimumCount, entry.MaximumCount));
	}
}
