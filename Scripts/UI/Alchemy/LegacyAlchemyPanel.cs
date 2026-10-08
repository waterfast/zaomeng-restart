using System;
using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.UI.Inventory;

namespace Zaomeng.UI.Alchemy;

public partial class LegacyAlchemyPanel : Node
{
	private static readonly (string Button, string Title, string Scene, Vector2 Position)[] Functions =
	[
		("qh", "强化", "LDL/strength", new(-315, -205)),
		("hc", "合成", "LDL/Synthesis", new(-315, -205)),
		("xq", "镶嵌", "LDL/Mosaic", new(-315, -205)),
		("hh", "幻化", "OtherScene/Transmogrified", new(-315, -205)),
		("fj", "分解", "LDL/Decompose", new(-305, -200)),
		("dz", "打造", "LDL/Forge", new(-340, -200))
	];
	private Node2D _root = null!;
	private Node2D? _page;
	private InventoryViewAdapter _adapter = null!;
	private LegacyBrowseInventory _backpack = null!;
	private Wallet _wallet = null!;
	public string CurrentFunction { get; private set; } = "";
	public event Action? CloseRequested;

	public void Bind(Node2D root, InventoryService inventory, ItemCatalog items, Wallet wallet)
	{
		_root = root;
		_wallet = wallet;
		_adapter = new InventoryViewAdapter(inventory, items);
		_backpack = new LegacyBrowseInventory(root.GetNode<Control>("Main_Backpack"), _adapter, wallet, items);
		root.GetNode<Button>("return").Pressed += () => CloseRequested?.Invoke();
		foreach (var function in Functions)
			root.GetNode<Button>(function.Button).Pressed += () => SelectFunction(function.Button);
		root.VisibilityChanged += OnVisibilityChanged;
		SelectFunction("qh");
		Refresh();
	}

	public void SelectFunction(string button)
	{
		int index = Array.FindIndex(Functions, item => item.Button == button);
		if (index < 0) throw new ArgumentException("未知炼丹炉功能。", nameof(button));
		var function = Functions[index];
		_backpack.HideTooltip();
		if (_page is not null) { _page.GetParent().RemoveChild(_page); _page.QueueFree(); }
		_page = GD.Load<PackedScene>($"res://Scenes/UI/{function.Scene}.tscn").Instantiate<Node2D>();
		_page.Position = function.Position;
		if (function.Button == "hh")
		{
			// 旧幻化是整屏弹窗，缩放到加工区域以保留背包与底部切页入口。
			_page.Scale = new Vector2(0.38f, 0.38f);
			_page.Position = new Vector2(-342, -218);
			// 旧页自带固定角色预览，目前没有幻化数据绑定，避免显示错误角色。
			_page.GetNode<CanvasItem>("BG/Player").Hide();
		}
		_root.GetNode("bg/bg2").AddChild(_page);
		// 只搬运展示，不让旧子页按钮暗示已有加工行为。
		foreach (Node node in _page.FindChildren("*", "BaseButton", true, false))
			if (node is BaseButton action) { action.Disabled = true; action.TooltipText = "此功能尚未开放"; }
		CurrentFunction = function.Title;
		foreach (var tab in Functions)
			_root.GetNode<Button>(tab.Button).AddThemeColorOverride("font_color", tab.Button == button ? Colors.Orange : Colors.White);
		var notice = new Label { Name = "PendingNotice", Text = $"{function.Title}功能尚未开放", Position = new Vector2(150, 525),
			MouseFilter = Control.MouseFilterEnum.Ignore };
		if (_root.GetNodeOrNull<Label>("PendingNotice") is { } previous) { _root.RemoveChild(previous); previous.QueueFree(); }
		notice.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Font/8_FZCuYuan-M03S.ttf"));
		notice.AddThemeFontSizeOverride("font_size", 14);
		_root.AddChild(notice);
	}

	private void Refresh()
	{
		_backpack.Refresh();
		_root.GetNode<Label>("bg/bg2/lh").Text = _wallet.Souls.ToString();
	}
	private void OnVisibilityChanged() { if (_root.Visible) Refresh(); else _backpack.HideTooltip(); }
	public override void _ExitTree()
	{
		if (_root is null) return;
		_root.VisibilityChanged -= OnVisibilityChanged;
		_backpack.Dispose();
		_adapter.Dispose();
	}
}
