using System;
using System.Collections.Generic;

namespace Zaomeng.Character;

/// <summary>一个可存档角色的身份、成长和配置；场上的动作及当前血量由运行层管理。</summary>
public sealed class Character
{
	public const int SkillSlotCount = 5;

	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public int Level { get; set; } = 1;
	public long Experience { get; set; }
	public CharacterStats BaseStats { get; set; } = new();
	public CharacterStats PermanentBonuses { get; set; } = new();
	public EquipmentLoadout Equipment { get; set; } = new();
	// 技能 ID 不在字典中表示未学会；等级至少为 1。
	public Dictionary<string, int> SkillLevels { get; set; } = new();
	// 固定索引对应技能快捷键 1～5；空字符串表示空槽。
	public List<string> EquippedSkillIds { get; set; } = new(["", "", "", "", ""]);
	public List<string> UnlockedTalentIds { get; set; } = new();

	public void LearnSkill(string skillId, int level = 1)
	{
		if (string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("技能 ID 不能为空。", nameof(skillId));
		if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
		SkillLevels[skillId] = level;
	}

	public void EquipSkill(int slot, string? skillId)
	{
		if (slot < 0 || slot >= SkillSlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
		if (!string.IsNullOrEmpty(skillId) && !SkillLevels.ContainsKey(skillId))
			throw new InvalidOperationException($"技能 {skillId} 尚未学会。");
		EquippedSkillIds[slot] = skillId ?? "";
	}
}
