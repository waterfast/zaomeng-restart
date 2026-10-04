using System;
using System.Linq;
using Godot;
using Zaomeng.Equipment;
using Zaomeng.Events;
using Zaomeng.Items;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng.UI.Inventory;

/// <summary>独立的基础镶嵌弹窗；只发请求，不自行扣宝石或修改装备。</summary>
public sealed class GemSocketView : IDisposable
{
	private readonly Window _window;
	private readonly VBoxContainer _body;
	private readonly InventoryViewAdapter _adapter;
	private readonly SaveCharacter _character;
	private readonly ItemCatalog _catalog;
	private readonly GameplayEvents _events;
	private readonly Action<string> _feedback;
	private readonly IDisposable _modifiedConnection;
	private readonly IDisposable _equipmentConnection;
	private string _instanceId = "";

	public GemSocketView(Node parent, InventoryViewAdapter adapter, SaveCharacter character,
		ItemCatalog catalog, GameplayEvents events, Action<string> feedback)
	{
		_adapter = adapter;
		_character = character;
		_catalog = catalog;
		_events = events;
		_feedback = feedback;
		_window = new Window
		{
			Name = "GemSocketWindow", Title = "宝石镶嵌", Size = new Vector2I(440, 300),
			Visible = false, Transient = true, ProcessMode = Node.ProcessModeEnum.Always
		};
		parent.AddChild(_window);
		var margin = new MarginContainer();
		_window.AddChild(margin);
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		foreach (string side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 16);
		var scroll = new ScrollContainer();
		margin.AddChild(scroll);
		_body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		scroll.AddChild(_body);
		_window.CloseRequested += Hide;
		_adapter.Changed += Refresh;
		_modifiedConnection = events.EquipmentModified.Subscribe(_ => Refresh());
		_equipmentConnection = events.EquipmentChanged.Subscribe(_ => Refresh());
	}

	public void Show(EquipmentInstance instance)
	{
		_instanceId = instance.InstanceId;
		_window.PopupCentered();
		Refresh();
	}

	public void Hide() => _window.Hide();

	private EquipmentInstance? FindTarget() => _character.Equipment.Slots.Values.FirstOrDefault(
		instance => instance.InstanceId == _instanceId) ?? _adapter.GetEntries(ItemCategory.Equipment)
		.Select(entry => entry.Equipment).FirstOrDefault(instance => instance?.InstanceId == _instanceId);

	private void Refresh()
	{
		if (!_window.Visible) return;
		EquipmentInstance? target = FindTarget();
		if (target is null) { Hide(); return; }
		foreach (Node child in _body.GetChildren()) { _body.RemoveChild(child); child.QueueFree(); }
		_catalog.TryGetDefinition(target.DefinitionId, out ItemDefinition? definition);
		_body.AddChild(new Label { Text = definition!.DisplayName });
		_body.AddChild(new Label { Text = "镶嵌消耗一颗宝石，拆卸将宝石放回背包。" });
		if (target.SocketedGemIds.Length == 0)
		{
			_body.AddChild(new Label { Text = "这件装备没有宝石孔。" });
			return;
		}
		InventoryDisplayEntry[] gems = _adapter.GetEntries(ItemCategory.Material)
			.Where(entry => entry.Definition is GemDefinition).ToArray();
		for (int i = 0; i < target.SocketedGemIds.Length; i++)
		{
			int socket = i;
			string gemId = target.SocketedGemIds[i];
			var row = new HBoxContainer { Name = $"Socket{socket}" };
			_body.AddChild(row);
			row.AddChild(new Label { Text = $"孔位 {socket + 1}" });
			if (gemId.Length > 0)
			{
				_catalog.TryGetDefinition(gemId, out ItemDefinition? gem);
				row.AddChild(new Label { Text = gem!.DisplayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
				var remove = new Button { Name = "Remove", Text = "拆卸" };
				row.AddChild(remove);
				remove.Pressed += () => Send(new(GemAction.Remove, target.InstanceId, socket, ExpectedGemId: gemId));
			}
			else
			{
				var options = new OptionButton { Name = "GemChoice", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
				row.AddChild(options);
				foreach (InventoryDisplayEntry entry in gems) options.AddItem($"{entry.Definition.DisplayName} ×{entry.Count}");
				if (gems.Length == 0) options.AddItem("背包没有可用宝石");
				var insert = new Button { Name = "Insert", Text = "镶嵌", Disabled = gems.Length == 0 };
				row.AddChild(insert);
				insert.Pressed += () =>
				{
					InventoryDisplayEntry entry = gems[options.Selected];
					Send(new(GemAction.Socket, target.InstanceId, socket, entry.SlotIndex, entry.Definition.Id));
				};
			}
		}
	}

	private void Send(GemRequest request)
	{
		GemResult result = _events.GemRequested.Send(request, this);
		if (!result.Success) _feedback(result.Message);
	}

	public void Dispose()
	{
		_adapter.Changed -= Refresh;
		_modifiedConnection.Dispose();
		_equipmentConnection.Dispose();
		_window.CloseRequested -= Hide;
		_window.QueueFree();
	}
}
