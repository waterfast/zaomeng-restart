using System;
using System.Linq;
using Godot;
using Zaomeng.Character;
using Zaomeng.Equipment;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

/// <summary>复用旧版说明场景；隐藏尚无数据的强化、五行、宝石条目。</summary>
public sealed class LegacyItemTooltip
{
	private const string InformationPath = "pro_wk/information/inf";
	private readonly Node2D _root;
	private readonly MarginContainer _layout;
	private readonly ColorRect _background;
	private readonly Panel _frame;
	private readonly Label _sockets;
	private readonly ItemDescriptionBinding _description;
	private Rect2 _slotArea;
	private int _revision;
	private readonly CharacterStatCatalog _statCatalog;
	private readonly VBoxContainer _registeredStats;

	public LegacyItemTooltip(Node parent)
	{
		_root = GD.Load<PackedScene>("res://Scenes/UI/BackPack/equipment_properies.tscn")
			.Instantiate<Node2D>();
		_root.Name = "ItemTooltip";
		_root.ZIndex = 100;
		_root.Hide();
		parent.AddChild(_root);
		_statCatalog = CharacterStatCatalog.Default;
		_registeredStats = new VBoxContainer { Name = "RegisteredStats", MouseFilter = Control.MouseFilterEnum.Ignore };
		var statsParent = _root.GetNode<VBoxContainer>(InformationPath);
		statsParent.AddChild(_registeredStats);
		statsParent.MoveChild(_registeredStats, 2);
		_root.GetNode<Control>($"{InformationPath}/VBoxContainer3").Hide();
		_layout = _root.GetNode<MarginContainer>("pro_wk");
		_background = _root.GetNode<ColorRect>("ColorRect");
		_frame = new Panel { Name = "TooltipFrame", MouseFilter = Control.MouseFilterEnum.Ignore };
		var frameStyle = new StyleBoxFlat { BgColor = new Color(0.025f, 0.018f, 0.01f, 0.96f),
			BorderColor = new Color(0.65f, 0.58f, 0.45f) };
		frameStyle.SetBorderWidthAll(1);
		_frame.AddThemeStyleboxOverride("panel", frameStyle);
		_root.AddChild(_frame);
		_root.MoveChild(_frame, _layout.GetIndex());
		_layout.Theme = new Theme { DefaultFont = GD.Load<Font>("res://Assets/Font/8_FZCuYuan-M03S.ttf"), DefaultFontSize = 17 };
		_description = ItemDescriptionBinding.Attach(_root.GetNode<Label>($"{InformationPath}/VBoxContainer/eq_ms"));
		_sockets = new Label { Name = "SocketSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		var information = _root.GetNode<VBoxContainer>(InformationPath);
		information.AddChild(_sockets);
		information.MoveChild(_sockets, information.GetChildCount() - 2);
		_sockets.Hide();
		_layout.Position = Vector2.Zero;
		_layout.CustomMinimumSize = new Vector2(240, 0);
		_layout.Size = new Vector2(240, 0);
		_background.Position = Vector2.Zero;
		// 说明框可能覆盖相邻物品，深色背景保证文字清晰。
		_background.Color = new Color(0, 0, 0, 0.94f);
		_layout.Resized += UpdateBackgroundAndPosition;
		_layout.MinimumSizeChanged += () => Callable.From(RefreshLayout).CallDeferred();
		SetIgnoreMouse(_root);
		ApplyCompactStyle(_layout);
		_background.Color = new Color(0.025f, 0.018f, 0.01f, 0.96f);
		_root.GetNode<Control>($"{InformationPath}/BsProp").Hide();
		// 原场景分隔线是按旧高度固定定位，隐藏后用容器间距适配动态条目。
		foreach (string path in new[] { "eq_pz/fgx", "eq_lx/fgx2", "have/fgx2" })
			_root.GetNode<CanvasItem>($"{InformationPath}/VBoxContainer2/{path}").Hide();
		foreach (string path in new[] { "eq_czl", "eq_star_level" })
			_root.GetNode<Control>($"{InformationPath}/VBoxContainer3/{path}").Hide();
	}

	public void Show(ItemDefinition item, int count, Rect2 slotArea,
		EquipmentInstance? instance = null, ItemCatalog? catalog = null, bool showOwnedCount = true)
	{
		_slotArea = slotArea;
		UpdateWidth();
		EquipmentDefinition? equipment = item as EquipmentDefinition;
		Set("VBoxContainer2/eq_name", item.DisplayName);
		Set("VBoxContainer2/eq_pz", equipment?.Quality is { Length: > 0 } quality ? $"品质：{quality}" : "");
		string type = item is GemDefinition ? "宝石" : equipment is null ? item.Category switch
		{
			ItemCategory.Equipment => "装备", ItemCategory.Material => "道具", _ => "消耗品"
		} : equipment.Slot switch
		{
			EquipmentSlot.Weapon => "武器", EquipmentSlot.Armor => "防具",
			EquipmentSlot.Accessory => "饰品", EquipmentSlot.Wing => "翅膀",
			EquipmentSlot.Title => "头衔", EquipmentSlot.Costume => "时装", _ => "法宝"
		};
		Set("VBoxContainer2/eq_lx", $"类型：{type}" +
			(equipment?.OwnerCaption is { Length: > 0 } owner ? $"·{owner}" : ""));
		Set("VBoxContainer2/have", !showOwnedCount || item.Category == ItemCategory.Equipment ? "" : $"拥有：{count} 个");
		foreach (string node in new[] { "eq_name", "eq_pz" })
		{
			Label label = _root.GetNode<Label>($"{InformationPath}/VBoxContainer2/{node}");
			label.AddThemeColorOverride("font_color", equipment?.QualityColor ?? Colors.White);
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
		}
		CharacterStats stats = instance is not null && catalog is not null
			? CharacterStatCalculator.CalculateEquipment(instance, catalog)
			: equipment?.GetStatBonuses() ?? (item as GemDefinition)?.GetStatBonuses() ?? new CharacterStats
			{ Attack = item.Attack, CriticalRating = item.CriticalRating, Accuracy = item.Accuracy };
		foreach (Node child in _registeredStats.GetChildren()) { _registeredStats.RemoveChild(child); child.QueueFree(); }
		foreach (var definition in _statCatalog.Definitions)
		{
			float value = definition.Read(stats);
			if (value == 0 || (item.Category != ItemCategory.Equipment && item is not GemDefinition)) continue;
			string number = definition.Format == StatDisplayFormat.Percentage ? $"{CharacterStatRegistry.Number(value * 100)}%" : CharacterStatRegistry.Number(value);
			_registeredStats.AddChild(new Label { Name = definition.Id,
				Text = $"{TranslationServer.Translate(definition.NameKey)}：{number}",
				AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore });
		}

		_description.Show(item);
		_sockets.Visible = instance is not null && instance.SocketedGemIds.Length > 0;
		_sockets.Text = instance is null ? "" : "宝石孔：\n" + string.Join("\n", instance.SocketedGemIds.Select((id, index) =>
			$"{index + 1}. " + (id.Length == 0 ? "空孔" : catalog is not null && catalog.TryGetDefinition(id, out ItemDefinition? gem)
				? gem!.DisplayName : id)));
		Set("VBoxContainer/eq_sj", $"售价：{item.SellPrice} 灵魂。");
		ApplyCompactStyle(_layout);
		foreach (Node child in _registeredStats.GetChildren())
			if (child is Label label) label.AddThemeColorOverride("font_color", new Color(1, 0.75f, 0.3f));
		_root.GetNode<Label>($"{InformationPath}/VBoxContainer/eq_sj")
			.AddThemeColorOverride("font_color", new Color(1, 0.75f, 0.3f));
		_root.Show();
		int revision = ++_revision;
		// 等待容器完成本帧排版，才能按实际高度夹紧，避免长描述越出屏幕。
		Callable.From(() =>
		{
			if (!_root.Visible || revision != _revision) return;
			RefreshLayout();
		}).CallDeferred();
	}

	public void Hide() { _revision++; _root.Hide(); }

	private void RefreshLayout()
	{
		if (!_root.Visible) return;
		_layout.ResetSize();
		UpdateBackgroundAndPosition();
	}

	private void UpdateBackgroundAndPosition()
	{
		if (!_root.Visible) return;
		// 自动换行和隐藏条目可能在后续排版才改变尺寸，背景与避让位置要跟随最终尺寸。
		_background.Size = _layout.Size;
		_frame.Size = _layout.Size;
		MoveBesideSlot();
	}

	private void UpdateWidth()
	{
		float scale = MathF.Abs(_root.GetParent<CanvasItem>().GetGlobalTransformWithCanvas().Scale.X);
		// 保持可阅读宽度，空间不足由定位逻辑换到格子左侧。
		float width = Math.Min(260, (_root.GetViewportRect().Size.X - 16) / Math.Max(scale, 0.01f));
		_layout.CustomMinimumSize = new Vector2(width, 0);
		_layout.Size = new Vector2(width, 0);
	}

	private void MoveBesideSlot()
	{
		Transform2D transform = _root.GetParent<CanvasItem>().GetGlobalTransformWithCanvas();
		Vector2 size = _layout.Size * transform.Scale.Abs();
		Vector2 viewport = _root.GetViewportRect().Size;
		float x = _slotArea.End.X + 8;
		if (x + size.X > viewport.X - 8) x = _slotArea.Position.X - size.X - 8;
		Vector2 position = new(Mathf.Clamp(x, 8, Mathf.Max(8, viewport.X - size.X - 8)),
			Mathf.Clamp(_slotArea.Position.Y, 8, Mathf.Max(8, viewport.Y - size.Y - 8)));
		_root.Position = transform.AffineInverse() * position;
	}

	private void Set(string path, string text)
	{
		Label label = _root.GetNode<Label>($"{InformationPath}/{path}");
		label.Text = text;
		label.Visible = text.Length > 0;
	}

	private static void SetIgnoreMouse(Node node)
	{
		if (node is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
		if (node is Label label) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		foreach (Node child in node.GetChildren()) SetIgnoreMouse(child);
	}

	private static void ApplyCompactStyle(Node node)
	{
		if (node is Label label)
		{
			label.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Font/8_FZCuYuan-M03S.ttf"));
			label.AddThemeFontSizeOverride("font_size", label.Name == "eq_name" ? 20 : 17);
			label.AddThemeConstantOverride("outline_size", 0);
			label.AddThemeConstantOverride("line_spacing", 2);
			label.HorizontalAlignment = HorizontalAlignment.Left;
		}
		if (node is BoxContainer box) box.AddThemeConstantOverride("separation", 5);
		foreach (Node child in node.GetChildren()) ApplyCompactStyle(child);
	}
}
