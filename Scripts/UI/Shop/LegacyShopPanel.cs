using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Shop;
using Zaomeng.UI.Inventory;

namespace Zaomeng.UI.Shop;

public partial class LegacyShopPanel : Node
{
	private const int PageSize = 9;
	private Node2D _root = null!;
	private ShopService _service = null!;
	private Wallet _wallet = null!;
	private Action _save = null!;
	private readonly List<Node2D> _cards = new();
	private readonly List<ShopOffer> _filtered = new();
	private LegacyItemTooltip _tooltip = null!;
	private AcceptDialog _feedback = null!;
	private string _filter = "Total";
	private int _page;
	public event Action? CloseRequested;

	public void Bind(Node2D root, ShopService service, Wallet wallet, string characterName, Action save)
	{
		_root = root;
		_service = service;
		_wallet = wallet;
		_save = save;
		_tooltip = new LegacyItemTooltip(root);
		_feedback = new AcceptDialog { Title = "商店" };
		root.AddChild(_feedback);
		root.GetNode<Label>("BG/BG2/bg_3/Role").Text = $"角色：{characterName}";
		root.GetNode<Button>("BG/Close").Pressed += () => CloseRequested?.Invoke();
		root.GetNode<BaseButton>("BG/BG2/Charge").Pressed += () => ShowFeedback("当前未开放充值。");
		root.GetNode<Label>("ColorRect/Label").Text = "提示：购买数量将全部加入共用背包。";
		root.GetNode<Label>("ColorRect2/Label").Text = "当前商店出售强化石，使用灵魂购买。";
		foreach (string tab in new[] { "Total", "zb", "dj", "sz", "cb" })
			root.GetNode<TextureButton>($"BG/BG2/ShopTypeChange/{tab}").Pressed += () =>
			{ _filter = tab; _page = 0; Refresh(); };
		var search = root.GetNode<LineEdit>("BG/BG2/HBoxContainer2/LineEdit");
		search.PlaceholderText = "输入商品名称";
		search.TextSubmitted += _ => { _page = 0; Refresh(); };
		root.GetNode<TextureButton>("BG/BG2/HBoxContainer2/qd").Pressed += () => { _page = 0; Refresh(); };
		root.GetNode<TextureButton>("BG/BG2/HBoxContainer/last").Pressed += () => { _page--; Refresh(); };
		root.GetNode<TextureButton>("BG/BG2/HBoxContainer/next").Pressed += () => { _page++; Refresh(); };
		root.VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	private void OnVisibilityChanged()
	{
		_tooltip.Hide();
		_feedback.Hide();
		if (_root.Visible) Refresh();
	}

	public void Refresh()
	{
		_tooltip.Hide();
		foreach (Node2D card in _cards) { card.GetParent().RemoveChild(card); card.QueueFree(); }
		_cards.Clear();
		_filtered.Clear();
		string query = _root.GetNode<LineEdit>("BG/BG2/HBoxContainer2/LineEdit").Text.Trim();
		foreach (ShopOffer offer in _service.Catalog.Offers)
		{
			bool category = _filter == "Total" || _filter == "dj" && offer.Item.Category != ItemCategory.Equipment ||
				_filter == "zb" && offer.Item.Category == ItemCategory.Equipment;
			if (category && offer.Item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)) _filtered.Add(offer);
		}
		int pages = Math.Max(1, (_filtered.Count + PageSize - 1) / PageSize);
		_page = Math.Clamp(_page, 0, pages - 1);
		_root.GetNode<Label>("BG/BG2/HBoxContainer/page").Text = $"{_page + 1}/{pages}";
		_root.GetNode<TextureButton>("BG/BG2/HBoxContainer/last").Disabled = _page == 0;
		_root.GetNode<TextureButton>("BG/BG2/HBoxContainer/next").Disabled = _page == pages - 1;
		foreach (string tab in new[] { "Total", "zb", "dj", "sz", "cb" })
			_root.GetNode<TextureButton>($"BG/BG2/ShopTypeChange/{tab}").TextureNormal = GD.Load<Texture2D>(
				tab == _filter ? "res://Assets/Art/Shop/shopTypeOnpress.png" : "res://Assets/Art/Shop/shopTypeNopress.png");
		RefreshBalances();
		var scene = GD.Load<PackedScene>("res://Scenes/UI/Shop/Shop_item.tscn");
		for (int i = 0; i < PageSize && _page * PageSize + i < _filtered.Count; i++)
		{
			ShopOffer offer = _filtered[_page * PageSize + i];
			Node2D card = scene.Instantiate<Node2D>();
			card.Position = new Vector2(-220 + (i % 3) * 220, -120 + (i / 3) * 100);
			_root.GetNode("BG/BG2").AddChild(card);
			_cards.Add(card);
			BindCard(card, offer);
		}
	}

	private void BindCard(Node2D card, ShopOffer offer)
	{
		card.GetNode<ColorRect>("BG/bgg").Color = Colors.Transparent;
		card.GetNode<Label>("BG/name_").Text = offer.Item.DisplayName;
		card.GetNode<Label>("BG/lh_value").Text = offer.UnitPrice.ToString();
		var currency = card.GetNode<Label>("BG/lh_value/lh_text");
		currency.Text = offer.Currency == CurrencyType.Soul ? "灵魂" : "点券";
		currency.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Font/8_FZCuYuan-M03S.ttf"));
		currency.AddThemeColorOverride("font_color", Colors.White);
		currency.AddThemeColorOverride("font_outline_color", Colors.Black);
		currency.AddThemeConstantOverride("outline_size", 1);
		var slot = new LegacyBrowseSlot(card.GetNode<Button>("BG/bgg/Items"), _tooltip);
		slot.Show(new InventoryDisplayEntry(0, offer.Item, 1, null));
		var quantity = card.GetNode<LineEdit>("BG/num_/num_kj/num_");
		quantity.MaxLength = 4;
		int ReadCount() => int.TryParse(quantity.Text, out int value) ? Math.Clamp(value, 1, ShopService.MaximumPurchaseCount) : 1;
		quantity.FocusExited += () => quantity.Text = ReadCount().ToString();
		card.GetNode<TextureButton>("BG/num_/num_kj/cure_num").Pressed += () => quantity.Text = Math.Min(ReadCount() + 1, ShopService.MaximumPurchaseCount).ToString();
		card.GetNode<TextureButton>("BG/num_/num_kj/reduce_num").Pressed += () => quantity.Text = Math.Max(ReadCount() - 1, 1).ToString();
		card.GetNode<Button>("BG/goumai").Pressed += () =>
		{
			if (!int.TryParse(quantity.Text, out int count) || count is < 1 or > ShopService.MaximumPurchaseCount)
			{ ShowFeedback("请输入 1 至 9999 的购买数量。"); return; }
			try
			{
				if (!_service.TryBuy(offer, count, out string message)) { ShowFeedback(message); return; }
				RefreshBalances();
				try { _save(); }
				catch (Exception error) { ShowFeedback($"购买已完成，但保存失败：{error.Message}。请重新保存。"); return; }
				ShowFeedback(message);
			}
			catch (Exception error) { ShowFeedback($"购买失败：{error.Message}"); }
		};
	}

	private void RefreshBalances()
	{
		SetBalance("Souls", "灵魂", _wallet.Souls);
		SetBalance("Coupons", "点券", _wallet.Coupons);
	}
	private void SetBalance(string nodeName, string caption, long amount)
	{
		var label = _root.GetNode<Label>($"BG/Balances/{nodeName}");
		label.Text = $"{caption}：{amount:N0}";
		// 钱包为 long；极大余额也完整显示，避免盖住另一种货币或越出边框。
		var font = label.GetThemeFont("font");
		int size = 22;
		while (size > 10 && font.GetStringSize(label.Text, fontSize: size).X > label.Size.X) size--;
		label.AddThemeFontSizeOverride("font_size", size);
	}
	private void ShowFeedback(string message) { _feedback.DialogText = message; _feedback.PopupCentered(); }
	public override void _ExitTree() { if (_root is not null) _root.VisibilityChanged -= OnVisibilityChanged; }
}
