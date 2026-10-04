using System;
using Godot;
using Zaomeng.Events;
using Zaomeng.Items;

namespace Zaomeng.UI.Inventory;

/// <summary>沿用旧版 sell_or_equ 场景；按钮由服务提供，不在界面判断道具能否使用。</summary>
public sealed class ItemActionMenu : IDisposable
{
	private readonly PopupPanel _popup;
	private readonly Control _content;
	private readonly VBoxContainer _list;
	private readonly GameplayEvents _events;
	private readonly Action<string> _feedback;
	private readonly Button _template;
	private readonly Texture2D _equipIcon;
	private readonly Texture2D _sellIcon;
	private readonly Texture2D _useIcon;
	private readonly Texture2D _actionBackground;
	private readonly Font _actionFont;
	public bool Visible => _popup.Visible;

	public ItemActionMenu(Node root, GameplayEvents events, Action<string> feedback)
	{
		_events = events;
		_feedback = feedback;
		_popup = new PopupPanel { Name = "ItemActionMenu", ProcessMode = Node.ProcessModeEnum.Always };
		_popup.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		root.AddChild(_popup);
		_content = GD.Load<PackedScene>("res://Scenes/UI/BackPack/sell_or_equ.tscn").Instantiate<Control>();
		_content.Name = "Options";
		_popup.AddChild(_content);
		_content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
		_content.Position = Vector2.Zero;
		_content.ShowBehindParent = false;
		_list = _content.GetNode<VBoxContainer>("VBoxContainer");
		_template = _list.GetNode<Button>("equ");
		_equipIcon = _template.Icon;
		_sellIcon = _list.GetNode<Button>("sell").Icon;
		_useIcon = GD.Load<Texture2D>("res://Assets/Art/BackPack/AllItems/330.png");
		_actionFont = GD.Load<Font>("res://Assets/Font/8_FZCuYuan-M03S.ttf");
		// 旧“装备/出售”图片已经带字；自定义操作使用同类金色底图叠字，避免落回默认白字按钮。
		_actionBackground = GD.Load<Texture2D>("res://Assets/Art/BackPack/zb_button_choose.png");
		_list.RemoveChild(_template);
		Button sell = _list.GetNode<Button>("sell");
		_list.RemoveChild(sell);
		sell.Free();
	}

	public void Show(ItemActionTarget target, Rect2 slotArea, int page = 0, int index = 0, string? caption = null)
	{
		foreach (Node child in _list.GetChildren())
			if (child.Name != "infor") { _list.RemoveChild(child); child.QueueFree(); }
		_list.GetNode<Label>("infor").Text = caption ?? $"第{page + 1}页第{index + 1}格";
		var options = _events.ItemActionRequested.GetOptions(target);
		foreach (ItemActionOption option in options)
		{
			Button button = (Button)_template.Duplicate();
			button.Name = option.Id;
			button.Icon = option.Id switch { "equip" => _equipIcon, "sell" => _sellIcon, "use" => _useIcon, _ => null };
			button.Text = button.Icon is null ? option.Label : "";
			button.Disabled = option.UnavailableReason.Length > 0;
			if (button.Icon is null) ApplyCustomActionStyle(button);
			button.TooltipText = button.Disabled ? option.UnavailableReason : option.Label;
			button.Pressed += () =>
			{
				Hide();
				ItemActionResult result = _events.ItemActionRequested.Send(new(option.Id, target), this);
				if (!result.Success) _feedback(result.Message);
				else if (result.Message.Length > 0) _feedback(result.Message);
			};
			_list.AddChild(button);
		}
		if (options.Count == 0)
		{
			var label = new Label { Name = "NoActions", Text = "暂无操作", HorizontalAlignment = HorizontalAlignment.Center };
			_list.AddChild(label);
		}
		float height = 24 + Math.Max(1, options.Count) * 30;
		_content.Size = new Vector2(62, height);
		_list.Size = new Vector2(55, height - 6);
		_content.GetNode<ColorRect>("Panel").Size = new Vector2(53, height);
		Sprite2D background = _content.GetNode<Sprite2D>("Panel/841");
		background.Position = new Vector2(26.3f, height / 2);
		background.Scale = new Vector2(0.697318f, 0.522145f * height / 68);
		Vector2 size = _content.Size * _content.Scale;
		Vector2 viewport = _popup.GetParent().GetViewport().GetVisibleRect().Size;
		Vector2 position = slotArea.End;
		position.X = Math.Clamp(position.X, 0, Math.Max(0, viewport.X - size.X));
		position.Y = Math.Clamp(position.Y, 0, Math.Max(0, viewport.Y - size.Y));
		_popup.Popup(new Rect2I((Vector2I)position, (Vector2I)size));
	}

	private void ApplyCustomActionStyle(Button button)
	{
		string text = button.Text;
		button.Text = "";
		// 与旧按钮图片保持同一可见区域；图层不参与最小尺寸计算，长文案不会撑大菜单。
		var background = new TextureRect
		{
			Name = "ActionBackground", Texture = _actionBackground,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SelfModulate = new Color(1, 1.3f, 0.06f, button.Disabled ? 0.45f : 1)
		};
		button.AddChild(background);
		background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		background.OffsetLeft = 2; background.OffsetRight = -2;
		background.OffsetTop = 4; background.OffsetBottom = -4;
		var caption = new Label
		{
			Name = "ActionCaption", Text = text,
			HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore, ClipText = true,
			SelfModulate = new Color(1, 1, 1, button.Disabled ? 0.45f : 1)
		};
		caption.AddThemeFontOverride("font", _actionFont);
		caption.AddThemeFontSizeOverride("font_size", 13);
		caption.AddThemeColorOverride("font_color", Colors.Black);
		button.AddChild(caption);
		caption.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
	}

	public void Hide() => _popup.Hide();
	public void Dispose() { _template.Free(); _popup.QueueFree(); }
}
