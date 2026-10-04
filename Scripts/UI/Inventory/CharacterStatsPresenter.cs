using System;
using Godot;
using Zaomeng.Character;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>按注册顺序分页映射角色属性，界面不保存或计算成长数据。</summary>
public sealed class CharacterStatsPresenter : IDisposable
{
	private static readonly (string Value, string Caption)[] NodeNames =
	[
		("hp", "Hp_tt"), ("mp", "Mp_tt"), ("att", "Att_tt"),
		("lucky", "Lucky_tt"), ("def", "Def_tt"), ("mdef", "Mdef_tt"),
		("crit", "Crit_tt"), ("miss", "misstt"), ("ehp", "ehp_tt"),
		("emp", "emp_tt")
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
	private readonly TextureButton _previousPage;
	private readonly TextureButton _nextPage;
	private readonly Label _previousPageCaption;
	private readonly Label _nextPageCaption;
	public CharacterStatRegistry Registry { get; }
	private int _page = 1;
	public int PageCount => Math.Max(1, (Registry.Entries.Count + NodeNames.Length - 1) / NodeNames.Length);

	public CharacterStatsPresenter(Node2D backpack, SaveCharacter character, Player player, ItemCatalog catalog,
		CharacterStatRegistry? registry = null)
	{
		Registry = registry ?? CharacterStatRegistry.CreateDefault();
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
		_previousPage = information.GetNode<TextureButton>("first");
		_nextPage = information.GetNode<TextureButton>("second");
		_previousPageCaption = AddNavigationCaption(_previousPage, "‹");
		_nextPageCaption = AddNavigationCaption(_nextPage, "›");
		_previousPage.Pressed += PreviousPage;
		_nextPage.Pressed += NextPage;
		Registry.Changed += Refresh;
		Refresh();
	}

	public void Refresh()
	{
		_page = Math.Clamp(_page, 1, PageCount);
		CharacterStats stats = CharacterStatCalculator.Calculate(_character, _catalog);
		for (int i = 0; i < NodeNames.Length; i++)
		{
			int index = (_page - 1) * NodeNames.Length + i;
			CharacterStatRegistry.Entry? row = index < Registry.Entries.Count ? Registry.Entries[index] : null;
			_captions[i].Text = row is null ? "" : TranslationServer.Translate(row.Caption).ToString();
			_values[i].Text = row is null ? "" : row.Format?.Invoke(row.Read(stats), _player)
				?? CharacterStatRegistry.Number(row.Read(stats));
		}
		_previousPage.Disabled = _page == 1;
		_nextPage.Disabled = _page == PageCount;
		_previousPage.TooltipText = $"上一页（{_page}/{PageCount}）";
		_nextPage.TooltipText = $"下一页（{_page}/{PageCount}）";
		// 超过两页时不再显示固定的 1、2，避免让玩家误以为没有第三页。
		_previousPageCaption.Visible = PageCount > 2;
		_nextPageCaption.Visible = PageCount > 2;
		UpdateLevel();
	}

	public void Dispose()
	{
		Registry.Changed -= Refresh;
		_previousPage.Pressed -= PreviousPage;
		_nextPage.Pressed -= NextPage;
		_previousPageCaption.QueueFree();
		_nextPageCaption.QueueFree();
	}

	private static Label AddNavigationCaption(TextureButton button, string text)
	{
		var caption = new Label
		{
			Text = text, HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		caption.AddThemeStyleboxOverride("normal", new StyleBoxFlat
		{
			BgColor = new Color("70441d"), BorderColor = new Color("dbaa59"),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2
		});
		caption.AddThemeColorOverride("font_color", new Color("ffe2a2"));
		button.AddChild(caption);
		caption.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return caption;
	}

	private void PreviousPage() => ShowPage(_page - 1);
	private void NextPage() => ShowPage(_page + 1);

	public void ShowPage(int page)
	{
		_page = Math.Clamp(page, 1, PageCount);
		Refresh();
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

}
