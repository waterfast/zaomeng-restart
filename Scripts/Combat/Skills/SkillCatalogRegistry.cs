using System;
using System.Collections.Generic;
using Godot;

namespace Zaomeng.Skills;

/// <summary>唯一目录装配入口；内容通过Registry.tres登记，C#只知道入口资源路径。</summary>
[GlobalClass]
public partial class SkillCatalogRegistry : Resource
{
	[Export] public Godot.Collections.Array<SkillCatalog> Catalogs { get; set; } = new();
	private readonly Dictionary<string, SkillCatalog> _byCharacter = new(StringComparer.Ordinal);
	private static SkillCatalogRegistry? _default;
	public static SkillCatalogRegistry Default
	{
		get
		{
			if (_default is not null) return _default;
			var registry = GD.Load<SkillCatalogRegistry>("res://Content/Skills/Registry.tres");
			if (registry is null) throw new InvalidOperationException("技能登记资源缺失。");
			registry.Validate();
			_default = registry;
			return registry;
		}
	}
	public void Validate()
	{
		_byCharacter.Clear();
		if (Catalogs is null || Catalogs.Count == 0) throw new InvalidOperationException("技能登记表没有角色目录。");
		foreach (SkillCatalog catalog in Catalogs)
		{
			if (catalog is null) throw new InvalidOperationException("技能登记表存在空目录。");
			catalog.Validate();
			if (!_byCharacter.TryAdd(catalog.CharacterId, catalog)) throw new InvalidOperationException($"重复登记角色 {catalog.CharacterId}。");
		}
	}
	public SkillCatalog Get(string characterId) => _byCharacter.TryGetValue(characterId, out SkillCatalog? catalog)
		? catalog : throw new InvalidOperationException($"角色 {characterId} 尚未登记技能目录。");
	public SkillCatalog? Find(string characterId) => _byCharacter.GetValueOrDefault(characterId);
}
