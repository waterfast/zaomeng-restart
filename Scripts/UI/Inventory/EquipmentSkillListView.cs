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
		if (item is not EquipmentDefinition { GrantedSkills.Count: > 0 } equipment) return;
		AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore });
		var heading = new Label { Name = "EquipmentSkillsHeading",
			Text = TranslationServer.Translate("EQUIPMENT_SKILLS_HEADING"), MouseFilter = MouseFilterEnum.Ignore };
		heading.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.35f));
		heading.AddThemeFontSizeOverride("font_size", 18);
		AddChild(heading);
		foreach (var skill in equipment.GrantedSkills)
		{
			var section = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			AddChild(section);
			var title = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			section.AddChild(title);
			title.AddChild(new TextureRect { Texture = skill.DisplayIcon, CustomMinimumSize = new(32, 32),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
			title.AddChild(new Label { Text = skill.DisplayName, SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ShrinkCenter, AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = MouseFilterEnum.Ignore });
			section.AddChild(new Label { Text = skill.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = MouseFilterEnum.Ignore });
		}
	}
}
