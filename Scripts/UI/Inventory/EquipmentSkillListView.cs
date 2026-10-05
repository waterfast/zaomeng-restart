using Godot;
using Zaomeng.Equipment;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

public partial class EquipmentSkillListView : VBoxContainer
{
	public void ShowSkills(ItemDefinition? item)
	{
		foreach (Node child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
		Visible = item is EquipmentDefinition { GrantedSkills.Count: > 0 };
		if (item is not EquipmentDefinition equipment) return;
		foreach (var skill in equipment.GrantedSkills)
		{
			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			AddChild(row);
			row.AddChild(new TextureRect { Texture = skill.DisplayIcon, CustomMinimumSize = new(32, 32),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore });
			var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			row.AddChild(text);
			text.AddChild(new Label { Text = $"{skill.DisplayName} Lv.1", AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore });
			text.AddChild(new Label { Text = skill.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore });
		}
	}
}
