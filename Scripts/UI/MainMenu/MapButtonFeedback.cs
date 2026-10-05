using System.Collections.Generic;
using Godot;

namespace Zaomeng.UI.MainMenu;

/// <summary>沿用旧地图黄色描边；当前关卡按原版一秒周期闪烁，交互时保持高亮。</summary>
public partial class MapButtonFeedback : Node
{
	private sealed class ButtonState
	{
		public required TextureButton Button { get; init; }
		public required ShaderMaterial Glow { get; init; }
		public bool Pulse { get; init; }
		public bool Hovered { get; set; }
		public bool Held { get; set; }
	}

	private readonly List<ButtonState> _buttons = new();
	private float _time;

	public void Bind(Node map, int currentLevel)
	{
		var shader = GD.Load<Shader>("res://Assets/Shaders/LevelButton.gdshader");
		foreach (Node node in map.FindChildren("*", "TextureButton", true, false))
		{
			var button = (TextureButton)node;
			if (button.Owner != map) continue;
			var glow = new ShaderMaterial { Shader = shader };
			glow.SetShaderParameter("line_color", Colors.Yellow);
			var state = new ButtonState { Button = button, Glow = glow, Pulse = button.Name == $"level_{currentLevel}" };
			_buttons.Add(state);
			button.Material = null;
			// 卷轴由多张子图片组成；让它们一起描边，文字则保留原来的字体描边。
			foreach (Node decoration in button.FindChildren("*", "", true, false))
			{
				if (decoration is Sprite2D sprite) sprite.UseParentMaterial = true;
				if (decoration is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
			}
			button.MouseEntered += () => { state.Hovered = true; Refresh(state); };
			button.MouseExited += () => { state.Hovered = false; Refresh(state); };
			button.ButtonDown += () => { state.Held = true; Refresh(state); };
			button.ButtonUp += () => { state.Held = false; Refresh(state); };
			button.VisibilityChanged += () =>
			{
				if (!button.IsVisibleInTree()) { state.Hovered = false; state.Held = false; }
				Refresh(state);
			};
			Refresh(state);
		}
	}

	public override void _Process(double delta)
	{
		_time = (_time + (float)delta) % 1;
		foreach (ButtonState state in _buttons) Refresh(state);
	}

	private void Refresh(ButtonState state)
	{
		bool active = !state.Button.Disabled && (state.Pulse || state.Hovered || state.Held);
		state.Button.Material = active ? state.Glow : null;
		if (!active) return;
		float thickness = state.Held ? 4 : state.Hovered ? 3 : 1 + 4 * (0.5f - Mathf.Abs(_time - 0.5f));
		state.Glow.SetShaderParameter("line_thickness", thickness);
	}
}
