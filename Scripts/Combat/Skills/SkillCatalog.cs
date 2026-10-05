using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Character;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Skills;

[GlobalClass]
public partial class SkillCatalog : Resource
{
	[Export] public string CharacterId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	[Export] public Texture2D Portrait { get; set; } = null!;
	[Export] public StringName SelectionAnimation { get; set; } = "";
	[Export] public PackedScene ActorScene { get; set; } = null!;
	[Export] public long[] ReservedKeys { get; set; } = [];
	[Export] public Godot.Collections.Array<SkillEntry> Skills { get; set; } = new();
	private readonly Dictionary<string, SkillEntry> _byId = new(StringComparer.Ordinal);
	public SkillEntry? Find(string id) => _byId.GetValueOrDefault(id);
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(CharacterId) || string.IsNullOrWhiteSpace(DisplayName) || Portrait is null || ActorScene is null || Skills is null)
			throw new InvalidOperationException($"角色技能目录 {CharacterId} 缺少角色信息或场景。");
		_byId.Clear();
		var reserved = new HashSet<long>();
		foreach (long key in ReservedKeys)
			if (key is < 65 or > 90 || !reserved.Add(key))
				throw new InvalidOperationException($"角色 {CharacterId} 的行为保留键无效或重复。");
		foreach (SkillEntry entry in Skills)
		{
			if (entry is null) throw new InvalidOperationException($"角色 {CharacterId} 有空技能条目。");
			entry.Validate();
			if (!_byId.TryAdd(entry.Id, entry)) throw new InvalidOperationException($"角色 {CharacterId} 重复登记技能 {entry.Id}。");
		}
		Node actor = ActorScene.Instantiate();
		try
		{
			if (actor is not Player || actor.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is not { } animator)
				throw new InvalidOperationException($"角色 {CharacterId} 缺少玩家脚本或动画播放器。");
			foreach (SkillEntry entry in Skills)
				if (entry.Action is { } action && !animator.HasAnimation(action.Animation))
					throw new InvalidOperationException($"角色 {CharacterId} 缺少技能动画 {action.Animation}。");
		}
		finally
		{
			// 未入树的临时场景必须释放原生节点树；Dispose 只释放 C# 句柄。
			actor.Free();
		}
	}
	public void ValidateCharacter(SaveCharacter character)
	{
		if (character.Id != CharacterId) throw new InvalidOperationException("角色与技能目录不匹配。");
		if (character.EquippedSkillIds.Count != SaveCharacter.SkillSlotCount || character.SkillKeyCodes.Count != SaveCharacter.SkillSlotCount)
			throw new InvalidOperationException("角色技能槽数量无效。");
		foreach (long key in character.SkillKeyCodes)
			if (Array.Exists(ReservedKeys, reserved => reserved == key))
				throw new InvalidOperationException($"角色 {CharacterId} 的技能键与行为保留键冲突。");
		foreach (var (id, level) in character.SkillLevels)
			if (Find(id) is not { } entry || level < 1 || level > entry.MaximumLevel)
				throw new InvalidOperationException($"角色 {CharacterId} 的已学技能 {id} 或等级未登记。");
		var equipped = new HashSet<string>();
		foreach (string id in character.EquippedSkillIds)
		{
			if (id.Length == 0) continue;
			if (Find(id) is not { Passive: false } || character.SkillLevels.GetValueOrDefault(id) < 1 || !equipped.Add(id))
				throw new InvalidOperationException($"角色 {CharacterId} 的装配技能 {id} 无效。");
		}
	}
	public void AddPassiveBonuses(SaveCharacter character, CharacterStats total)
	{
		ValidateCharacter(character);
		foreach (var (id, level) in character.SkillLevels)
			foreach (PassiveSkillBonus bonus in Find(id)!.PassiveBonuses) bonus.AddTo(total, SkillLevelResolver.Effective(level, total.SkillLevelBonus, Find(id)!.MaximumLevel));
	}
}
