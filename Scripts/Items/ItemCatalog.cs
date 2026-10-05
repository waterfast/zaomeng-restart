using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Equipment;
using Zaomeng.Equipment.Skills;

namespace Zaomeng.Items;

/// <summary>物品定义的唯一目录，同时把最大堆叠数提供给纯 C# 背包。</summary>
[GlobalClass]
[Tool]
public partial class ItemCatalog : Resource, IItemCatalog
{
	[Export] public Godot.Collections.Array<ItemDefinition> Definitions { get; set; } = new();

	public bool TryGetDefinition(string itemId, out ItemDefinition? definition)
	{
		foreach (ItemDefinition? candidate in Definitions)
		{
			if (candidate?.Id == itemId)
			{
				definition = candidate;
				return true;
			}
		}
		definition = null;
		return false;
	}

	public bool TryGetMaxStack(string itemId, out int maxStack)
	{
		if (TryGetDefinition(itemId, out ItemDefinition? definition))
		{
			maxStack = definition!.MaxStack;
			return true;
		}
		maxStack = 0;
		return false;
	}

	public void Validate()
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		var skillsById = new Dictionary<string, EquipmentSkillDefinition>(StringComparer.Ordinal);
		foreach (ItemDefinition? definition in Definitions)
		{
			if (definition is null)
				throw new InvalidOperationException("物品目录包含空定义。");
			if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id))
				throw new InvalidOperationException($"物品 ID 为空或重复：{definition.Id}");
			if (definition.MaxStack < 1)
				throw new InvalidOperationException($"物品 {definition.Id} 的最大堆叠数必须大于零。");
			if (definition.SellPrice < 0) throw new InvalidOperationException($"物品 {definition.Id} 的售价不能为负数。");
			if (definition is ConsumableDefinition consumable)
			{
				if (consumable.Category != ItemCategory.Consumable || !Enum.IsDefined(consumable.Effect))
					throw new InvalidOperationException($"消耗品 {definition.Id} 的类别或效果无效。");
				if (consumable.Effect == ConsumableEffect.GrantSouls && consumable.SoulsGranted <= 0)
					throw new InvalidOperationException($"药水 {definition.Id} 的灵魂奖励必须大于零。");
				if (consumable.Effect == ConsumableEffect.RandomMaterialChest)
				{
					if (consumable.RewardItemIds.Length == 0 || consumable.RewardMinCount < 1 ||
						consumable.RewardMaxCount < consumable.RewardMinCount || consumable.RewardMaxCount > 9999)
						throw new InvalidOperationException($"宝箱 {definition.Id} 的奖励配置无效。");
					foreach (string id in consumable.RewardItemIds)
						if (!TryGetDefinition(id, out ItemDefinition? reward) || reward!.Category != ItemCategory.Material)
							throw new InvalidOperationException($"宝箱 {definition.Id} 的奖励 {id} 必须是目录中的材料。");
				}
			}
			if (definition.Category == ItemCategory.Equipment && definition is not EquipmentDefinition)
				throw new InvalidOperationException($"装备 {definition.Id} 必须使用 EquipmentDefinition。");
			if (definition is EquipmentDefinition equipment && (equipment.Category != ItemCategory.Equipment ||
				equipment.MaxStack != 1 || equipment.GemSocketCount < 0 || equipment.GemSocketCount > 8 || !Enum.IsDefined(equipment.Rarity)))
				throw new InvalidOperationException($"装备 {definition.Id} 的类型、堆叠数或宝石孔配置无效。");
			if (definition is GemDefinition gem && (gem.Category != ItemCategory.Material ||
				!Enum.IsDefined(gem.Attribute) || !float.IsFinite(gem.Bonus) || gem.Bonus < 0))
				throw new InvalidOperationException($"宝石 {definition.Id} 的类型或属性配置无效。");
			if (definition is EquipmentDefinition skillEquipment)
			{
				if (skillEquipment.GrantedSkills is null)
					throw new InvalidOperationException($"装备 {definition.Id} 的授予技能列表为空。");
				var grantedIds = new HashSet<string>(StringComparer.Ordinal);
				foreach (EquipmentSkillDefinition skill in skillEquipment.GrantedSkills)
				{
					if (skill is null) throw new InvalidOperationException($"装备 {definition.Id} 包含空技能。");
					skill.Validate();
					if (!grantedIds.Add(skill.Id)) throw new InvalidOperationException($"装备 {definition.Id} 重复授予技能 {skill.Id}。");
					if (skillsById.TryGetValue(skill.Id, out EquipmentSkillDefinition? existing) && existing != skill)
						throw new InvalidOperationException($"装备技能 ID {skill.Id} 对应不同定义。");
					skillsById[skill.Id] = skill;
				}
			}
		}
	}

	public bool TryGetEquipmentSocketCount(string itemId, out int socketCount)
	{
		if (TryGetDefinition(itemId, out ItemDefinition? item) && item is EquipmentDefinition equipment)
		{
			socketCount = equipment.GemSocketCount;
			return true;
		}
		socketCount = 0;
		return false;
	}

	public bool IsGem(string itemId) => TryGetDefinition(itemId, out ItemDefinition? item) && item is GemDefinition;

	public void ValidateEquipmentInstance(EquipmentInstance instance, EquipmentSlot? slot = null)
	{
		if (!instance.IsValid || !TryGetDefinition(instance.DefinitionId, out ItemDefinition? item) ||
			item is not EquipmentDefinition equipment || instance.SocketedGemIds.Length != equipment.GemSocketCount ||
			(slot.HasValue && equipment.Slot != slot.Value))
			throw new InvalidOperationException($"装备实例 {instance.InstanceId} 与物品定义或槽位不匹配。");
		foreach (string gemId in instance.SocketedGemIds)
			if (gemId.Length > 0 && !IsGem(gemId))
				throw new InvalidOperationException($"装备实例 {instance.InstanceId} 引用了未知宝石 {gemId}。");
	}
}
