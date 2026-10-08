using System;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Items;
using Zaomeng.Events;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>旧版法宝详情，仅展示当前装备；未实现的操作保留布局但禁用。</summary>
public sealed class MagicWeaponView : IDisposable
{
	private readonly Node2D _root;
	private readonly Node2D _help;
	private readonly Node _parent;
	private readonly SaveCharacter _character;
	private readonly ItemCatalog _catalog;
	private readonly Sprite2D _fallback;
	private readonly IDisposable? _equipmentConnection;
	public Node2D Root => _root;
	public event Action? CloseRequested;
	public MagicWeaponView(Node parent, ItemCatalog catalog, SaveCharacter character, GameplayEvents? events = null)
	{
		_parent = parent; _catalog = catalog; _character = character;
		_root = GD.Load<PackedScene>("res://Scenes/UI/BackPack/Magic_weapon_infor.tscn").Instantiate<Node2D>();
		_root.Name = "MagicWeaponPanel"; _root.ZIndex = 105;
		parent.AddChild(_root); _root.Hide();
		_root.GetNode<BaseButton>("BG/Close").Pressed += () => CloseRequested?.Invoke();
		_root.VisibilityChanged += OnVisibilityChanged;
		foreach (string path in new[] { "BG/bg_2/up_level", "BG/szfb", "BG/wxxl", "BG/gjwx", "BG/czlxl" })
			_root.GetNode<BaseButton>(path).Disabled = true;
		_fallback = new Sprite2D { Position = _root.GetNode<Node2D>("BG/Icon").Position };
		_root.GetNode("BG").AddChild(_fallback);
		_help = GD.Load<PackedScene>("res://Scenes/UI/MagicWeapon/Magic_help.tscn").Instantiate<Node2D>();
		_help.Name = "MagicWeaponHelp"; _help.ZIndex = 106;
		parent.AddChild(_help); _help.Hide();
		_help.GetNode<BaseButton>("BG/Close").Pressed += _help.Hide;
		_root.GetNode<BaseButton>("BG/tips").Pressed += () => { Center(_help); _help.Show(); };
		_equipmentConnection = events?.EquipmentChanged.Subscribe(change => { if (change.CharacterId == character.Id) Refresh(); });
	}
	private void Center(Node2D panel)
	{
		Transform2D transform = _parent is CanvasItem canvas ? canvas.GetGlobalTransformWithCanvas() : panel.GetCanvasTransform();
		panel.Position = transform.AffineInverse() * (panel.GetViewportRect().Size / 2);
	}
	public void Show() { Center(_root); Refresh(); _root.Show(); }
	public void Hide() { _root.Hide(); _help.Hide(); }
	private void OnVisibilityChanged()
	{
		if (_root.Visible) { Center(_root); Refresh(); }
		else _help.Hide();
	}
	public void Refresh()
	{
		EquipmentDefinition? item = null;
		if (_character.Equipment.Get(EquipmentSlot.MagicWeapon) is { } instance &&
			_catalog.TryGetDefinition(instance.DefinitionId, out ItemDefinition? definition)) item = definition as EquipmentDefinition;
		MagicWeaponPresentation? presentation = item?.MagicWeaponPresentation;
		_root.GetNode<Label>("BG/Name_").Text = item?.DisplayName ?? "未装备法宝";
		foreach (string name in new[] { "m_level", "m_czl", "m_wx" }) SetStat(name, "—");
		SetStat("m_Hp", Format(item?.HealthBonus)); SetStat("m_Mp", Format(item?.ManaBonus));
		SetStat("m_Power", Format(item?.Attack)); SetStat("m_Def", Format(item?.PhysicalDefenseBonus));
		SetStat("m_MDef", Format(item?.MagicDefenseBonus));
		_root.GetNode<TextureProgressBar>("BG/bg_2/lh_bar").Value = 0;
		_root.GetNode<Label>("BG/bg_2/lh_bar/lh_value").Text = "—";
		_root.GetNode<Label>("BG/MagicWeaponSkillTitle").Text = presentation?.SkillName ?? "";
		string skillDescription = presentation?.SkillDescription ?? "";
		if (item?.MagicWeaponAbility is { } ability)
			skillDescription += $"\n按 H 施放 · 魔耗 {ability.Action.GetManaCost(1)} · 冷却 {ability.Action.CooldownSeconds:0.#} 秒\n当前为基础版，五行与成长尚未接入。";
		else if (item is not null) skillDescription += "\n主动效果尚未接入。";
		_root.GetNode<Label>("BG/ScrollContainer/VBoxContainer/MagicWeaponSkill").Text = skillDescription;
		_root.GetNode<Label>("BG/ScrollContainer/VBoxContainer/PsTitle").Text = string.IsNullOrEmpty(presentation?.PassiveDescription) ? "" : "被动技能";
		_root.GetNode<Label>("BG/ScrollContainer/VBoxContainer/MagicWeaponSkill2").Text = presentation?.PassiveDescription ?? "";
		var skillIcon = _root.GetNode<Sprite2D>("BG/Icon_");
		skillIcon.Texture = presentation?.SkillIcon; skillIcon.Visible = skillIcon.Texture is not null;
		var animation = _root.GetNode<AnimationPlayer>("BG/IconPlayer");
		animation.Stop();
		var icon = _root.GetNode<AnimatedSprite2D>("BG/Icon");
		icon.Stop(); icon.Hide();
		icon.GetNode<CanvasItem>("Tooth").Hide();
		_fallback.Hide();
		if (presentation is not null && animation.HasAnimation(presentation.Animation))
		{ icon.Show(); animation.Play(presentation.Animation); animation.Advance(0); }
		else if (item?.Icon is { } texture)
		{ _fallback.Texture = texture; _fallback.Scale = new Vector2(64f / texture.GetWidth(), 64f / texture.GetHeight()); _fallback.Show(); }
	}
	private void SetStat(string name, string value) => _root.GetNode<Label>($"BG/bg_2/{name}").Text = value;
	private static string Format(float? value) => value?.ToString("0.##") ?? "—";
	public void Dispose()
	{
		_equipmentConnection?.Dispose();
		if (GodotObject.IsInstanceValid(_root)) _root.QueueFree();
		if (GodotObject.IsInstanceValid(_help)) _help.QueueFree();
	}
}
