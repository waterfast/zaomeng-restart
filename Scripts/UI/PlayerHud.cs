using Godot;

namespace Zaomeng.UI;

/// <summary>绑定迁入的 HUD；玩家拥有属性和成长，界面仅刷新显示。</summary>
public partial class PlayerHud : Node
{
	private Player _player = null!;
	private TextureProgressBar _health = null!;
	private TextureProgressBar _mana = null!;
	private TextureProgressBar _experience = null!;
	private Label _healthText = null!;
	private Label _manaText = null!;
	private Label _experienceText = null!;
	private Label _level = null!;
	private Label _healthRecovery = null!;
	private Label _manaRecovery = null!;

	public void Bind(Node2D hud, Player player)
	{
		_player = player;
		Node bars = hud.GetNode("roleLayer/role_hp_mp_exp");
		_health = bars.GetNode<TextureProgressBar>("hp_bar");
		_mana = bars.GetNode<TextureProgressBar>("mp_bar");
		_experience = bars.GetNode<TextureProgressBar>("exp_bar");
		_healthText = _health.GetNode<Label>("hp_text");
		_manaText = _mana.GetNode<Label>("mp_text");
		_experienceText = _experience.GetNode<Label>("exp_text");
		_level = bars.GetNode<Label>("role_level");
		_healthRecovery = _health.GetNode<Label>("nature_recovery_hp");
		_manaRecovery = _mana.GetNode<Label>("nature_recovery_mp");
		_health.GetNode<TextureProgressBar>("hp_bar2").Value = 0;
		string portrait = player.CharacterId switch
		{
			"role_2" => "tsz", "role_3" => "zbj", "role_4" => "shs", "role_5" => "blm", _ => "swk"
		};
		hud.GetNode<Sprite2D>("roleLayer/role_head").Texture =
			GD.Load<Texture2D>($"res://Assets/Art/HeroPicture/RoleProperiesBox/{portrait}.png");
		Refresh();
	}

	public override void _Process(double delta) => Refresh();

	private void Refresh()
	{
		if (_player is null) return;
		SetBar(_health, _player.Health, _player.MaxHealth);
		SetBar(_mana, _player.Mana, _player.MaxMana);
		SetBar(_experience, _player.IsMaximumLevel ? 1 : _player.Experience,
			_player.IsMaximumLevel ? 1 : _player.ExperienceToNextLevel);
		_healthText.Text = $"{_player.Health:0}/{_player.MaxHealth:0}";
		_manaText.Text = $"{_player.Mana:0}/{_player.MaxMana:0}";
		_experienceText.Text = _player.IsMaximumLevel ? "满级" : $"{_player.Experience}/{_player.ExperienceToNextLevel}";
		_level.Text = _player.Level.ToString();
		float healthRecovery = _player.CalculatedStats?.HealthRegeneration ?? 0;
		float manaRecovery = _player.CalculatedStats?.ManaRegeneration ?? 0;
		_healthRecovery.Text = !_player.IsDead && _player.Health < _player.MaxHealth && healthRecovery > 0 ? $"+{healthRecovery:0.##}/s" : "";
		_manaRecovery.Text = !_player.IsDead && _player.Mana < _player.MaxMana && manaRecovery > 0 ? $"+{manaRecovery:0.##}/s" : "";
	}

	private static void SetBar(TextureProgressBar bar, double value, double maximum)
	{
		bar.MaxValue = maximum > 0 ? maximum : 1;
		bar.Value = value;
	}
}
