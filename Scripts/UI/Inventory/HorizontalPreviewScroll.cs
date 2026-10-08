using System;
using Godot;

namespace Zaomeng.UI.Inventory;

/// <summary>横向预览支持鼠标拖动与滚轮；不占用背包的物品操作。</summary>
public partial class HorizontalPreviewScroll : Node
{
	private ScrollContainer _scroll = null!;
	private Action _hideTooltip = null!;
	private bool _pressed;
	private bool _dragging;
	private Vector2 _origin;
	private int _startScroll;

	public void Bind(ScrollContainer scroll, Action hideTooltip)
	{
		_scroll = scroll;
		_hideTooltip = hideTooltip;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.GetHScrollBar().ValueChanged += _ => hideTooltip();
	}

	public override void _Input(InputEvent input)
	{
		if (!_scroll.IsVisibleInTree()) { _pressed = false; return; }
		if (input is not InputEventMouse mouseEvent) return;
		Vector2 mouse = _scroll.GetGlobalTransformWithCanvas().AffineInverse() * mouseEvent.Position;
		bool inside = new Rect2(Vector2.Zero, _scroll.Size).HasPoint(mouse);
		if (input is InputEventMouseButton button)
		{
			if (button.ButtonIndex == MouseButton.Left)
			{
				if (button.Pressed && inside && mouse.Y < _scroll.Size.Y - _scroll.GetHScrollBar().Size.Y)
				{
					_pressed = true;
					_dragging = false;
					_origin = mouse;
					_startScroll = _scroll.ScrollHorizontal;
				}
				else if (!button.Pressed) _pressed = false;
			}
			if (button.Pressed && inside && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
			{
				_scroll.ScrollHorizontal += button.ButtonIndex == MouseButton.WheelDown ? 56 : -56;
				GetViewport().SetInputAsHandled();
			}
		}
		if (input is InputEventMouseMotion && _pressed)
		{
			if (Mathf.Abs(mouse.X - _origin.X) > 5) _dragging = true;
			if (!_dragging) return;
			_hideTooltip();
			_scroll.ScrollHorizontal = _startScroll + (int)(_origin.X - mouse.X);
			GetViewport().SetInputAsHandled();
		}
	}
}
