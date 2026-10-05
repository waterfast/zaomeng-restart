using Godot;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

/// <summary>描述绑定随语言切换刷新；生命周期跟随描述控件，无需订阅全局事件。</summary>
public partial class ItemDescriptionBinding : Node
{
	private Label _label = null!;
	private ItemDefinition? _item;
	private EquipmentSkillListView _skills = null!;

	public static ItemDescriptionBinding Attach(Label label)
	{
		var binding = new ItemDescriptionBinding { _label = label };
		binding._skills = new EquipmentSkillListView { Name = "EquipmentSkills", MouseFilter = Control.MouseFilterEnum.Ignore };
		label.GetParent().AddChild(binding._skills);
		label.GetParent().MoveChild(binding._skills, label.GetIndex() + 1);
		label.AddChild(binding);
		return binding;
	}

	public void Show(ItemDefinition? item)
	{
		_item = item;
		Refresh();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && _label is not null) Refresh();
	}

	private void Refresh()
	{
		_label.Text = _item?.Description ?? "";
		_label.Visible = _label.Text.Length > 0;
		_skills.ShowSkills(_item);
		if (_label.GetParent() is not Container)
		{
			_skills.Position = _label.Position + new Vector2(0, _label.GetMinimumSize().Y + 8);
			_skills.Size = new(_label.Size.X, 0);
		}
	}
}
