using Godot;
using Zaomeng.Settings;

namespace Zaomeng.UI;

/// <summary>使用旧版 BossBlood 美术显示实际战斗血量。</summary>
public partial class BossHud : Node
{
	private TextureProgressBar _bar = null!;
	private Monster? _boss;
	public void Bind(CanvasLayer layer)
	{
		_bar = GD.Load<PackedScene>("res://Scenes/UI/Level/BossBlood.tscn").Instantiate<TextureProgressBar>();
		layer.AddChild(_bar);
		_bar.MouseFilter = Control.MouseFilterEnum.Ignore;
		_bar.Hide();
	}
	public void Track(Monster? boss) => _boss = boss;
	public override void _Process(double delta)
	{
		_bar.Visible = GameSettings.IsEnabled(GameOption.BossHealthBar) && IsInstanceValid(_boss) && !_boss!.IsDead;
		if (!_bar.Visible) return;
		_bar.Position = new(GetViewport().GetVisibleRect().Size.X - 370, 60);
		_bar.MaxValue = _boss!.MaxHealth;
		_bar.Value = GameSettings.IsEnabled(GameOption.SmoothHealthBars)
			? Mathf.Lerp((float)_bar.Value, _boss.Health, 1 - Mathf.Exp(-12 * (float)delta)) : _boss.Health;
		_bar.GetNode<Label>("MonsterName").Text = _boss.DisplayName;
		_bar.GetNode<Label>("BloodValue").Text = $"{_boss.Health:0}/{_boss.MaxHealth:0}";
		_bar.GetNode<Label>("BloodValue").Visible = GameSettings.IsEnabled(GameOption.MonsterHealthNumbers);
	}
}
