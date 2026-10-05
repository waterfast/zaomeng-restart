using System.Collections.Generic;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Equipment.Skills;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

/// <summary>背包详情与悬停共用；读取授予技能的游戏数据，不在 UI 或装备中复制效果文案。</summary>
public static class ItemDescriptionBuilder
{
	public static string Build(ItemDefinition item)
	{
		var sections = new List<string>();
		if (item.Description.Length > 0) sections.Add(item.Description);
		if (item is EquipmentDefinition equipment && equipment.GrantedSkills.Count > 0)
		{
			sections.Add(TranslationServer.Translate("EQUIPMENT_SKILLS_HEADING").ToString());
			foreach (EquipmentSkillDefinition skill in equipment.GrantedSkills)
				sections.Add($"{skill.DisplayName}\n{skill.Description}");
		}
		return string.Join("\n\n", sections);
	}
}
