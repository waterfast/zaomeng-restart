using System;
using System.Linq;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Events;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>复用旧法宝列表，穿戴仍走统一装备请求，不持有第二份法宝状态。</summary>
public sealed class MagicWeaponView : IDisposable
{
	private readonly Node2D _root;
	private readonly InventoryViewAdapter _inventory;
	private readonly SaveCharacter _character;
	private readonly GameplayEvents _events;
	private readonly Action<string> _feedback;
	private readonly Node2D _parent;
	private readonly EquipmentDefinition[] _definitions;
	private readonly Node2D[] _rows = new Node2D[3];
	private int _page;
	public MagicWeaponView(Node2D parent, InventoryViewAdapter inventory, ItemCatalog catalog,
		SaveCharacter character, GameplayEvents events, Action<string> feedback)
	{
		_parent = parent; _inventory = inventory; _character = character; _events = events; _feedback = feedback;
		_definitions = catalog.Definitions.OfType<EquipmentDefinition>().Where(item => item.Slot == EquipmentSlot.MagicWeapon).ToArray();
		_root = GD.Load<PackedScene>("res://Scenes/UI/BackPack/Use_magic_weapon.tscn").Instantiate<Node2D>();
		_root.Name = "MagicWeaponPanel"; _root.Position = new(490, 310); _root.ZIndex = 105;
		parent.AddChild(_root); _root.Hide();
		_root.GetNode<Label>("BG/chooseSkill").Text = "选择法宝进行穿戴（法宝主动技能、五行与成长尚未迁入）";
		_root.GetNode<BaseButton>("BG/Close").Pressed += Hide;
		_root.GetNode<BaseButton>("BG/HBoxContainer/Last").Pressed += () => { _page--; Refresh(); };
		_root.GetNode<BaseButton>("BG/HBoxContainer/Next").Pressed += () => { _page++; Refresh(); };
		for (int i = 0; i < _rows.Length; i++)
		{
			int row = i;
			_rows[i] = GD.Load<PackedScene>("res://Scenes/UI/BackPack/skill_select.tscn").Instantiate<Node2D>();
			_rows[i].Position = new(0, -110 + i * 120);
			_root.GetNode("BG").AddChild(_rows[i]);
			_rows[i].GetNode<BaseButton>("BG/choose").Pressed += () => Choose(row);
		}
	}
	public void Show()
	{
		_root.Position = _parent.GetGlobalTransformWithCanvas().AffineInverse() * (_parent.GetViewportRect().Size / 2);
		Refresh(); _root.Show();
	}
	public void Hide() => _root.Hide();
	public void Refresh()
	{
		int pages = Math.Max(1, (_definitions.Length + 2) / 3);
		_page = Math.Clamp(_page, 0, pages - 1);
		_root.GetNode<Label>("BG/HBoxContainer/PageInfor").Text = $"{_page + 1}/{pages}";
		_root.GetNode<BaseButton>("BG/HBoxContainer/Last").Disabled = _page == 0;
		_root.GetNode<BaseButton>("BG/HBoxContainer/Next").Disabled = _page == pages - 1;
		for (int i = 0; i < 3; i++)
		{
			var row = _rows[i]; int index = _page * 3 + i;
			row.Visible = index < _definitions.Length;
			if (!row.Visible) continue;
			var item = _definitions[index];
			bool equipped = _character.Equipment.Get(EquipmentSlot.MagicWeapon)?.DefinitionId == item.Id;
			bool owned = equipped || _inventory.GetEntries(ItemCategory.Equipment).Any(entry => entry.Definition.Id == item.Id);
			var icon = row.GetNode<Sprite2D>("BG/SkillIcon"); icon.Texture = item.Icon;
			if (item.Icon is not null) icon.Scale = new Vector2(56f / item.Icon.GetWidth(), 56f / item.Icon.GetHeight());
			// 子标签无需随原图尺寸缩放，名称在右侧统一展示。
			row.GetNode<Label>("BG/SkillIcon/Name_").Hide();
			row.GetNode<Label>("BG/SkillName").Text = item.DisplayName;
			row.GetNode<Label>("BG/SkillName/Level").Hide();
			row.GetNode<Label>("BG/Skill_Infor").Text = item.Description;
			row.GetNode<CanvasItem>("BG/isChoose").Visible = equipped;
			row.GetNode<Label>("BG/NoHave").Text = owned ? "" : "未拥有";
			row.GetNode<BaseButton>("BG/choose").Disabled = !owned;
			row.GetNode<Label>("BG/choose/choose_").Text = equipped ? "卸下" : "穿戴";
		}
	}
	private void Choose(int row)
	{
		int index = _page * 3 + row;
		if (index >= _definitions.Length) return;
		var item = _definitions[index];
		EquipmentResult result;
		if (_character.Equipment.Get(EquipmentSlot.MagicWeapon) is { } current && current.DefinitionId == item.Id)
			result = _events.EquipmentRequested.Send(new(EquipmentAction.Unequip, ExpectedInstanceId: current.InstanceId, Slot: EquipmentSlot.MagicWeapon), this);
		else
		{
			var entry = _inventory.GetEntries(ItemCategory.Equipment).FirstOrDefault(value => value.Definition.Id == item.Id);
			if (entry?.Equipment is null) { Refresh(); return; }
			result = _events.EquipmentRequested.Send(new(EquipmentAction.Equip, entry.SlotIndex, entry.Equipment.InstanceId), this);
		}
		if (!result.Success) _feedback(result.Message);
		Refresh();
	}
	public void Dispose() => _root.QueueFree();
}
