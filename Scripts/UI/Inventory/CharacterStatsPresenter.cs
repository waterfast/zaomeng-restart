using System;
using System.Globalization;
using Godot;
using Zaomeng.Character;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>把角色档案映射到旧背包的两页属性格，界面不保存或计算成长数据。</summary>
public sealed class CharacterStatsPresenter
{
	private sealed record StatLine(string Caption, Func<CharacterStats, float> Read,
		float RatingDenominator = 0, bool IsPercent = false);

	private static readonly (string Value, string Caption)[] NodeNames =
	[
		("hp", "Hp_tt"), ("mp", "Mp_tt"), ("att", "Att_tt"),
		("lucky", "Lucky_tt"), ("def", "Def_tt"), ("mdef", "Mdef_tt"),
		("crit", "Crit_tt"), ("miss", "misstt"), ("ehp", "ehp_tt"),
		("emp", "emp_tt")
	];

	private static readonly StatLine?[] FirstPage =
	[
		new("生命", stats => stats.MaxHealth),
		new("魔法", stats => stats.MaxMana),
		new("攻击", stats => stats.Attack),
		new("幸运", stats => stats.Luck, 50),
		new("物防", stats => stats.PhysicalDefense, 250),
		new("魔防", stats => stats.MagicDefense, 250),
		new("暴击", stats => stats.CriticalRating, 100),
		new("闪避", stats => stats.DodgeRating, 100),
		new("回血", stats => stats.HealthRegeneration),
		new("回魔", stats => stats.ManaRegeneration)
	];

	private static readonly StatLine?[] SecondPage =
	[
		new("命中", stats => stats.Accuracy),
		new("韧性", stats => stats.Toughness),
		new("吸血", stats => stats.LifeSteal, IsPercent: true),
		new("破甲", stats => stats.ArmorPenetration),
		new("暴免", stats => stats.CriticalResistance),
		new("破魔", stats => stats.MagicPenetration),
		null, null, null, null
	];

	private readonly Label[] _values = new Label[NodeNames.Length];
	private readonly Label[] _captions = new Label[NodeNames.Length];
	private readonly SaveCharacter _character;
	private readonly Player _player;
	private readonly ItemCatalog _catalog;
	private readonly TextureRect _firstLevelDigit;
	private readonly TextureRect _secondLevelDigit;
	private readonly HBoxContainer _levelDigits;
	private readonly TextureProgressBar _experienceBar;
	private readonly Label _experienceText;
	private int _page = 1;

	public CharacterStatsPresenter(Node2D backpack, SaveCharacter character, Player player, ItemCatalog catalog)
	{
		_character = character;
		_player = player;
		_catalog = catalog;
		Node information = backpack.GetNode("background/infomation");
		for (int i = 0; i < NodeNames.Length; i++)
		{
			_values[i] = information.GetNode<Label>(NodeNames[i].Value);
			_captions[i] = _values[i].GetNode<Label>(NodeNames[i].Caption);
		}
		_levelDigits = information.GetNode<HBoxContainer>("leve_background/Level_Show");
		_firstLevelDigit = _levelDigits.GetNode<TextureRect>("Number_1");
		_secondLevelDigit = _levelDigits.GetNode<TextureRect>("Number_2");
		_experienceBar = information.GetNode<TextureProgressBar>("exp_bar");
		_experienceText = _experienceBar.GetNode<Label>("exp_text");
		information.GetNode<TextureButton>("first").Pressed += () => ShowPage(1);
		information.GetNode<TextureButton>("second").Pressed += () => ShowPage(2);
		Refresh();
	}

	public void Refresh()
	{
		StatLine?[] rows = _page == 1 ? FirstPage : SecondPage;
		for (int i = 0; i < rows.Length; i++)
		{
			StatLine? row = rows[i];
			_captions[i].Text = row is null ? "" : TranslationServer.Translate(row.Caption).ToString();
			_values[i].Text = row is null ? "" : FormatRow(row, i);
		}
		UpdateLevel();
	}

	private void ShowPage(int page)
	{
		_page = page;
		Refresh();
	}

	private string FormatRow(StatLine row, int index)
	{
		float value = row.Read(CharacterStatCalculator.Calculate(_character, _catalog));
		if (_page == 1 && index == 0)
			return $"{Number(_player.Health)}/{Number(value)}";
		if (_page == 1 && index == 1)
			return $"{Number(value)}/{Number(value)}"; // 当前魔法量尚无运行时字段。
		if (row.IsPercent)
			return $"{Number(value * 100)}%";
		if (row.RatingDenominator > 0)
		{
			float rating = Math.Max(0, value);
			float percent = rating / (rating + row.RatingDenominator) * 100;
			return $"{Number(value)}({Number(MathF.Round(percent, 1))}%)";
		}
		return Number(value);
	}

	private void UpdateLevel()
	{
		int level = Math.Clamp(_character.Level, 1, 99);
		if (level < 10)
		{
			_levelDigits.Position = new Vector2(-15, -15);
			_firstLevelDigit.Texture = LevelDigit(level);
			_secondLevelDigit.Texture = null;
		}
		else
		{
			_levelDigits.Position = new Vector2(-35, -15);
			_firstLevelDigit.Texture = LevelDigit(level / 10);
			_secondLevelDigit.Texture = LevelDigit(level % 10);
		}

		long threshold = CharacterProgression.ExperienceToNextLevel(_character.Level);
		_experienceBar.MaxValue = threshold;
		_experienceBar.Value = Math.Min(_character.Experience, threshold);
		_experienceText.Text = $"{_character.Experience}/{threshold}";
	}

	private static Texture2D LevelDigit(int digit) =>
		GD.Load<Texture2D>($"res://Assets/Art/AllNumber/Level/Level_{digit}.png");

	private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
