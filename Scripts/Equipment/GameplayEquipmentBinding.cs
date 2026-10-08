using System;
using Godot;
using Zaomeng.Events;
using Zaomeng.Inventory;
using Zaomeng.Items;
using Zaomeng.Save;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.Equipment;

/// <summary>关卡生命周期内连接服务与事件资源；属性应用、通知和保存由此协调。</summary>
public sealed class GameplayEquipmentBinding : IDisposable
{
	private readonly EquipmentService _equipment;
	private readonly GemSocketService _gems;
	private readonly InventoryService _inventory;
	private readonly GameplayEvents _events;
	private readonly Action _refreshStats;
	private readonly Action _save;
	private readonly IDisposable _requestConnection;
	private readonly IDisposable _gemConnection;
	private readonly IDisposable _itemConnection;
	public ItemActionService ItemActions { get; }
	private bool _executing;

	public GameplayEquipmentBinding(SaveCharacter character, ItemCatalog catalog,
		InventoryService inventory, GameplayEvents events, Action refreshStats, Action save, Wallet wallet, Func<Player?>? activePlayer = null)
	{
		events.Validate();
		_equipment = new(character, catalog, inventory);
		_gems = new(character, catalog, inventory);
		ItemActions = new(inventory, catalog, character, wallet, _equipment, activePlayer);
		_inventory = inventory;
		_events = events;
		_refreshStats = refreshStats;
		_save = save;
		_requestConnection = events.EquipmentRequested.RegisterHandler(Execute);
		try { _gemConnection = events.GemRequested.RegisterHandler(ExecuteGem); }
		catch { _requestConnection.Dispose(); throw; }
		try { _itemConnection = events.ItemActionRequested.RegisterHandler(ExecuteItem, ItemActions.GetOptions); }
		catch { _requestConnection.Dispose(); _gemConnection.Dispose(); throw; }
		_inventory.Changed += OnInventoryChanged;
	}

	private void OnInventoryChanged()
	{
		_refreshStats();
		_events.InventoryChanged.Raise(this);
	}

	private EquipmentResult Execute(EquipmentRequest request)
	{
		if (_executing) return EquipmentResult.Busy();
		_executing = true;
		try
		{
			EquipmentResult result = _equipment.Execute(request);
			if (result.Change is not null)
			{
				SaveChanges();
				_events.EquipmentChanged.Raise(result.Change, this);
			}
			return result;
		}
		finally { _executing = false; }
	}

	private GemResult ExecuteGem(GemRequest request)
	{
		if (_executing) return GemResult.Busy();
		_executing = true;
		try
		{
			GemResult result = _gems.Execute(request);
			if (result.Change is not null)
			{
				SaveChanges();
				_events.EquipmentModified.Raise(result.Change, this);
			}
			return result;
		}
		finally { _executing = false; }
	}

	private ItemActionResult ExecuteItem(ItemActionRequest request)
	{
		if (_executing) return ItemActionResult.Busy();
		_executing = true;
		try
		{
			ItemActionResult result = ItemActions.Execute(request);
			if (result.Success && result.Changed)
			{
				SaveChanges();
				if (result.EquipmentChange is not null) _events.EquipmentChanged.Raise(result.EquipmentChange, this);
				_events.ItemActionCompleted.Raise(this);
			}
			return result;
		}
		finally { _executing = false; }
	}

	private void SaveChanges()
	{
		// 写盘失败不撤销已完成的装备操作；单独通知用户。
		try { _save(); }
		catch (Exception error)
		{
			GD.PushError($"物品已变更，但保存失败：{error}");
			_events.SaveFailed.Raise("物品已变更，但存档保存失败，请稍后重试保存。", this);
		}
	}

	public void Dispose()
	{
		_requestConnection.Dispose();
		_gemConnection.Dispose();
		_itemConnection.Dispose();
		_inventory.Changed -= OnInventoryChanged;
	}
}
