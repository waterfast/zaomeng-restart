using System;
using System.Collections.Generic;
using System.IO;
using Zaomeng.Character;
using Zaomeng.Equipment;

namespace Zaomeng.Save;

public static class SaveDataValidator
{
	public static void Validate(GameSaveData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		if (data.ClaimedQuestIds is null) throw new InvalidDataException("任务领取状态无效。");
		foreach (string id in data.ClaimedQuestIds)
			if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("任务领取标识不能为空。");
		if (data.Version != GameSaveData.CurrentVersion)
			throw new NotSupportedException($"存档版本 {data.Version} 不受支持，当前版本为 {GameSaveData.CurrentVersion}。");
		if (data.Inventory is null || data.Inventory.Capacity <= 0 ||
			data.Inventory.Slots is null || data.Inventory.Slots.Count != data.Inventory.Capacity)
			throw new InvalidDataException("存档中的背包容量或槽位数无效。");
		var instanceIds = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < data.Inventory.Slots.Count; i++)
		{
			ItemStackSaveData? stack = data.Inventory.Slots[i];
			if (stack is not null && (string.IsNullOrWhiteSpace(stack.ItemId) || stack.Count <= 0))
				throw new InvalidDataException($"存档中第 {i} 格的物品无效。");
			if (stack?.Equipment is EquipmentInstance instance)
			{
				if (stack.Count != 1 || stack.ItemId != instance.DefinitionId)
					throw new InvalidDataException($"存档中第 {i} 格的装备与物品不一致。");
				ValidateInstance(instance, instanceIds);
			}
		}
		if (data.Wallet is null || data.Wallet.Souls < 0 || data.Wallet.Coupons < 0)
			throw new InvalidDataException("存档中的货币余额无效。");
		if (data.UnlockedLevel is < 1 or > 4)
			throw new InvalidDataException("存档中的关卡进度无效。");
		if (data.Characters is null)
			throw new InvalidDataException("存档中的角色列表无效。");
		var characterIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (Zaomeng.Character.Character character in data.Characters)
		{
			if (character is null || string.IsNullOrWhiteSpace(character.Id) ||
				!characterIds.Add(character.Id) || character.Level < 1 || character.Experience < 0 ||
				character.Name is null || character.BaseStats is null ||
				character.PermanentBonuses is null || character.Equipment is null ||
				character.SkillLevels is null || character.EquippedSkillIds is null ||
				character.SkillKeyCodes is null || character.SkillKeyCodes.Count != Zaomeng.Character.Character.SkillSlotCount ||
				character.UnlockedTalentIds is null ||
				character.EquippedSkillIds.Count != Zaomeng.Character.Character.SkillSlotCount ||
				!HasFiniteStats(character.BaseStats) || !HasFiniteStats(character.PermanentBonuses))
				throw new InvalidDataException("存档中的角色档案无效。");
			if (character.Equipment.Slots is null)
				throw new InvalidDataException($"角色 {character.Id} 的装备配置无效。");
			foreach (var (slot, instance) in character.Equipment.Slots)
			{
				if (!Enum.IsDefined(slot)) throw new InvalidDataException("无效的穿戴槽位。");
				ValidateInstance(instance, instanceIds);
			}
			foreach (var (skillId, level) in character.SkillLevels)
				if (string.IsNullOrWhiteSpace(skillId) || level < 1)
					throw new InvalidDataException($"角色 {character.Id} 的已学技能无效。");
			foreach (string skillId in character.EquippedSkillIds)
				if (skillId is null || (skillId.Length > 0 && !character.SkillLevels.ContainsKey(skillId)))
					throw new InvalidDataException($"角色 {character.Id} 的技能快捷栏无效。");
			var skillKeys = new HashSet<long>();
			foreach (long key in character.SkillKeyCodes)
				if (key is < 65 or > 90 || !skillKeys.Add(key) || "ADJKRCVBNH".Contains((char)key))
					throw new InvalidDataException($"角色 {character.Id} 的技能按键配置无效。");
			foreach (string talentId in character.UnlockedTalentIds)
				if (string.IsNullOrWhiteSpace(talentId))
					throw new InvalidDataException($"角色 {character.Id} 的天赋 ID 无效。");
		}
	}

	private static void ValidateInstance(EquipmentInstance instance, HashSet<string> ids)
	{
		if (instance is null || !instance.IsValid || !ids.Add(instance.InstanceId))
			throw new InvalidDataException("装备实例无效或同时出现在多个背包/角色槽位中。");
	}

	private static bool HasFiniteStats(CharacterStats stats) =>
		float.IsFinite(stats.MaxHealth) && float.IsFinite(stats.MaxMana) &&
		float.IsFinite(stats.Attack) && float.IsFinite(stats.PhysicalDefense) &&
		float.IsFinite(stats.MagicDefense) && float.IsFinite(stats.CriticalRating) &&
		float.IsFinite(stats.DodgeRating) && float.IsFinite(stats.HealthRegeneration) &&
		float.IsFinite(stats.ManaRegeneration) && float.IsFinite(stats.LifeSteal) &&
		float.IsFinite(stats.Luck) && float.IsFinite(stats.Toughness) &&
		float.IsFinite(stats.Accuracy) && float.IsFinite(stats.CriticalResistance) &&
		float.IsFinite(stats.ArmorPenetration) && float.IsFinite(stats.MagicPenetration) &&
		float.IsFinite(stats.HasteRating) && float.IsFinite(stats.SkillLevelBonus);
}
