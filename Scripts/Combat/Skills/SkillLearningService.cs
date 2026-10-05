using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Save;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Skills;

/// <summary>学习与装配规则集中处理，主地图和局内面板使用同一角色配置。</summary>
public sealed class SkillLearningService
{
	private readonly SaveCharacter character;
	private readonly Wallet wallet;
	public SkillCatalog Catalog { get; }
	public SkillLearningService(SaveCharacter character, Wallet wallet)
	{
		this.character = character;
		this.wallet = wallet;
		Catalog = SkillCatalogRegistry.Default.Get(character.Id);
		Catalog.ValidateCharacter(character);
	}
	public SaveCharacter Character => character;
	public long Souls => wallet.Souls;
	public int Level(string id) => character.SkillLevels.GetValueOrDefault(id);

	public bool TryLearn(string id, out string message)
	{
		SkillEntry? skill = Catalog.Find(id);
		if (skill is null) { message = "当前角色无法学习此技能"; return false; }
		int level = Level(id);
		if (level >= skill.MaximumLevel) { message = "技能已满级"; return false; }
		if (!wallet.TrySpend(CurrencyType.Soul, skill.LearningCost(level))) { message = "灵魂不足"; return false; }
		character.LearnSkill(id, level + 1);
		message = $"已{(level == 0 ? "学会" : "升级")} {skill.DisplayName}";
		return true;
	}

	public bool TryEquip(int slot, string id, out string message)
	{
		SkillEntry? skill = Catalog.Find(id);
		if (slot is < 0 or >= SaveCharacter.SkillSlotCount || skill is null || skill.Passive || Level(id) < 1)
		{ message = "请先学习主动技能"; return false; }
		// 一个技能只占一个槽，换键时移走原来的位置。
		for (int i = 0; i < SaveCharacter.SkillSlotCount; i++)
			if (character.EquippedSkillIds[i] == id) character.EquipSkill(i, "");
		character.EquipSkill(slot, id);
		message = $"{skill.DisplayName} 已设置到 {KeyName(slot)}";
		return true;
	}

	public string KeyName(int slot) => OS.GetKeycodeString((Key)character.SkillKeyCodes[slot]);

	public bool TrySetKey(int slot, Key key, out string message)
	{
		if (slot is < 0 or >= SaveCharacter.SkillSlotCount || key is < Key.A or > Key.Z)
		{ message = "请选择 A～Z 字母键"; return false; }
		if (Array.Exists(Catalog.ReservedKeys, reserved => reserved == (long)key)) { message = "该按键已被角色技能行为占用"; return false; }
		for (int i = 0; i < SaveCharacter.SkillSlotCount; i++)
			if (i != slot && character.SkillKeyCodes[i] == (long)key)
			{ message = "该按键已被另一个技能槽使用"; return false; }
		foreach (StringName action in InputMap.GetActions())
		{
			if (action.ToString().StartsWith("skill_", StringComparison.Ordinal) || action.ToString().StartsWith("ui_", StringComparison.Ordinal)) continue;
			foreach (InputEvent input in InputMap.ActionGetEvents(action))
				if (input is InputEventKey binding && (binding.PhysicalKeycode == key || binding.Keycode == key))
				{ message = "该按键已用于移动、攻击或菜单"; return false; }
		}
		character.SkillKeyCodes[slot] = (long)key;
		message = $"技能槽 {slot + 1} 按键已改为 {KeyName(slot)}";
		return true;
	}

	public void Apply(Player? player)
	{
		Catalog.ValidateCharacter(character);
		if (player is not null && player.CharacterId != Catalog.CharacterId) throw new InvalidOperationException("不能把技能应用到另一角色。");
		for (int i = 0; i < SaveCharacter.SkillSlotCount; i++)
		{
			StringName action = $"skill_{i + 1}";
			InputMap.ActionEraseEvents(action);
			InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = (Key)character.SkillKeyCodes[i] });
			player?.SetEquippedSkill(i, Catalog.Find(character.EquippedSkillIds[i])?.Action);
		}
		player?.RefreshCharacterStats();
	}
}
