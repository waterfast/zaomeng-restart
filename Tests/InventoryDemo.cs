using Godot;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.UI.Inventory;

namespace Zaomeng;

/// <summary>独立演示场景。只负责提供测试数据与测试按键，不进入正式游戏流程。</summary>
public partial class InventoryDemo : Node
{
	[Export] public ItemCatalog Catalog { get; set; } = null!;

	private BackpackView _view = null!;
	private InventoryService _inventory = null!;
	private InventoryViewAdapter _adapter = null!;
	private Label _hint = null!;
	private int _nextItem;

	public override void _Ready()
	{
		_view = GetNode<BackpackView>("BackpackView");
		_hint = GetNode<Label>("Hint");
		_inventory = InventorySample.Create(Catalog);
		_adapter = new InventoryViewAdapter(_inventory, Catalog);
		_view.Bind(_adapter);
		_view.CloseRequested += () => _view.Hide();
		_nextItem = Catalog.Definitions.Count;
		UpdateHint($"已放入 {Catalog.Definitions.Count} 件测试物品");
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		switch (key.Keycode)
		{
			case Key.A:
				AddNextItem();
				break;
			case Key.F:
				FillNextPage();
				break;
			case Key.R:
				RemoveFirstItem();
				break;
			case Key.C:
				_view.Visible = !_view.Visible;
				break;
		}
	}

	public override void _ExitTree() => _adapter?.Dispose();

	private void AddNextItem()
	{
		ItemDefinition item = Catalog.Definitions[_nextItem++ % Catalog.Definitions.Count];
		UpdateHint(_inventory.AddItem(item.Id, 1) ? $"加入 {item.DisplayName}" : "背包已满");
	}

	private void RemoveFirstItem()
	{
		ItemDefinition item = Catalog.Definitions[0];
		UpdateHint(_inventory.RemoveItem(item.Id, 1)
			? $"移除了一件{item.DisplayName}"
			: $"背包中没有{item.DisplayName}");
	}

	private void FillNextPage()
	{
		while (_adapter.GetEntries(ItemCategory.Equipment).Count <= 35)
		{
			ItemDefinition item = Catalog.Definitions[_nextItem++ % Catalog.Definitions.Count];
			if (!_inventory.AddItem(item.Id, 1))
				break;
		}
		UpdateHint("已填充到第二页，可点击翻页按钮");
	}

	private void UpdateHint(string status) =>
		_hint.Text = $"{status}  ·  A 添加  F 填满一页  R 移除首件物品  C 开关背包";
}
