using Godot;
using Zaomeng.Skills;
using Zaomeng.Settings;

namespace Zaomeng.UI;

/// <summary>绑定迁入的 HUD；玩家拥有属性和成长，界面仅刷新显示。</summary>
public partial class PlayerHud : Node
{
	private Player _player = null!;
	private TextureProgressBar _health = null!;
	private TextureProgressBar _mana = null!;
	private TextureProgressBar _experience = null!;
	private TextureProgressBar _wushuang = null!;
	private AnimatedSprite2D _wushuangReady = null!;
	private Label _healthText = null!;
	private Label _manaText = null!;
	private Label _experienceText = null!;
	private Label _level = null!;
	private Label _healthRecovery = null!;
	private Label _manaRecovery = null!;
	private readonly TextureRect[] _skillIcons = new TextureRect[5];
	private readonly TextureRect[] _skillSlots = new TextureRect[5];
	private SkillLearningService? _skills;

	public void Bind(Node2D hud, Player player, SkillLearningService? skills = null)
	{
		_player = player;
		_skills = skills;
		_wushuang = hud.GetNode<TextureProgressBar>("roleLayer/role_menu/ws_wk/ws_effect");
		_wushuangReady = hud.GetNode<AnimatedSprite2D>("roleLayer/role_menu/max_ws");
		_wushuang.MaxValue = Zaomeng.Combat.Wushuang.WushuangRuntime.Maximum;
		var buffs = new BuffBar { Name = "ActiveBuffs", MouseFilter = Control.MouseFilterEnum.Pass };
		buffs.Bind(player);
		// 使用原场景角色框下方的 Buff_box，位置随 HUD 一起调整。
		hud.GetNode<HBoxContainer>("roleLayer/role_hp_mp_exp/Buff_box").AddChild(buffs);
		string[] keys = ["Y", "U", "I", "O", "L"];
		for (int i = 0; i < keys.Length; i++)
		{
			_skillSlots[i] = hud.GetNode<TextureRect>($"roleLayer/role_menu/SkillBox/{keys[i]}");
			_skillIcons[i] = _skillSlots[i].GetNode<TextureRect>("Icon");
			_skillIcons[i].ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_skillIcons[i].StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		}
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
		// 当前战斗系统没有旧版护盾值，不能显示模板中残留的空护盾条。
		bars.GetNode<TextureProgressBar>("RoleProtect").Hide();
		hud.GetNode<Sprite2D>("roleLayer/role_head").Texture = SkillCatalogRegistry.Default.Get(player.CharacterId).Portrait;
		Refresh();
	}

	private double _frameDelta;
	public override void _Process(double delta) { _frameDelta = delta; Refresh(); }

	private void Refresh()
	{
		if (_player is null) return;
		_wushuang.Value = _player.Wushuang.Value;
		_wushuangReady.Visible = _player.Wushuang.IsReady;
		_wushuang.TooltipText = $"无双 {_player.Wushuang.Value}/100 · 空格开启";
		for (int i = 0; i < _skillIcons.Length; i++)
		{
			SkillDefinition? skill = _player.GetEquippedSkill(i);
			TextureRect icon = _skillIcons[i];
			TextureRect slot = _skillSlots[i];
			SkillEntry? entry = skill is null ? null : _skills?.Catalog.Find(skill.Id);
			icon.Texture = entry?.DisplayIcon;
			slot.GetNode<Label>("Y").Text = _skills?.KeyName(i) ?? new[] { "Y", "U", "I", "O", "L" }[i];
			float cooldown = skill is null ? 0 : _player.GetSkillCooldown(skill);
			slot.GetNode<Label>("TimeText").Text = cooldown > 0 ? $"{cooldown:0.0}" : "";
			var shade = slot.GetNode<TextureProgressBar>("PicBox");
			shade.TextureProgress = icon.Texture;
			shade.MaxValue = skill is null ? 1 : Mathf.Max(0.001f, _player.GetSkillCooldownDuration(skill));
			shade.Value = cooldown;
			slot.TooltipText = skill is null ? "空技能槽：先学习并设置按键" : $"{entry?.DisplayName} Lv.{_player.EffectiveSkillLevel(skill.Id)} · 消耗 {skill.GetManaCost(_player.EffectiveSkillLevel(skill.Id))} 魔法";
		}
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

	private void SetBar(TextureProgressBar bar, double value, double maximum)
	{
		bar.MaxValue = maximum > 0 ? maximum : 1;
		bar.Value = _frameDelta > 0 && GameSettings.IsEnabled(GameOption.SmoothHealthBars)
			? Mathf.Lerp((float)bar.Value, (float)value, 1 - Mathf.Exp(-12 * (float)_frameDelta)) : value;
	}
}
