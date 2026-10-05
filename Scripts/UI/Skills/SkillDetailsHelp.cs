using Godot;

namespace Zaomeng.UI;

/// <summary>独立的数值提示入口；阻止点击穿透到技能装配按钮。</summary>
public partial class SkillDetailsHelp : Button
{
	public override Control _MakeCustomTooltip(string forText)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.04f, 0.02f, 1),
			BorderColor = new Color(0.6f, 0.4f, 0.15f),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
			ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
		});
		var text = new Label { Text = forText, CustomMinimumSize = new Vector2(350, 0),
			AutowrapMode = TextServer.AutowrapMode.WordSmart };
		text.AddThemeFontSizeOverride("font_size", 16);
		text.AddThemeColorOverride("font_color", Colors.White);
		panel.AddChild(text);
		return panel;
	}
}
