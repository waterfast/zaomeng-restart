using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.UI.Inventory;

namespace Zaomeng.Character;

[GlobalClass]
public partial class CharacterStatCatalog : Resource
{
	private static CharacterStatCatalog? _default;
	public static CharacterStatCatalog Default => _default ??= GD.Load<CharacterStatCatalog>("res://Content/GameData/Stats/Registry.tres")
		?? throw new InvalidOperationException("属性展示目录缺失。");
	[Export] public Godot.Collections.Array<CharacterStatDefinition> Definitions { get; set; } = new();
	public CharacterStatRegistry CreateRegistry()
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		var registry = new CharacterStatRegistry();
		foreach (CharacterStatDefinition definition in Definitions)
		{
			if (definition is null) throw new InvalidOperationException("属性目录包含空条目。");
			definition.Validate();
			if (!ids.Add(definition.Id)) throw new InvalidOperationException($"属性 ID 重复：{definition.Id}");
			registry.Register(definition.CreateEntry());
		}
		return registry;
	}
}
