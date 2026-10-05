using System;
using Godot;
using Zaomeng.Events;

namespace Zaomeng.UI.Inventory;

public sealed class BulkSaleView : IDisposable
{
	private readonly Control _root;
	private readonly BaseButton _entry;
	private readonly Node2D _parent;
	private readonly MarginContainer _layout;
	public BulkSaleView(Node2D parent, GameplayEvents events, Action<string> feedback)
	{
		_parent = parent;
		_root = GD.Load<PackedScene>("res://Scenes/UI/BackPack/pl_sell.tscn").Instantiate<Control>();
		_root.Name = "BulkSale";
		parent.AddChild(_root);
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
		_root.Size = new(240, 300);
		_root.ZIndex = 110;
		_root.Hide();
		const string path = "MarginContainer/VBoxContainer/";
		var range = _root.GetNode<LineEdit>(path + "page_");
		range.Text = "整个背包";
		range.Editable = false;
		var white = _root.GetNode<CheckBox>(path + "pt");
		white.Text = "普通装备（白装）";
		white.ButtonPressed = true;
		white.Disabled = true;
		foreach (string id in new[] { "yx", "jl", "ss", "xl", "hq", "cs", "sq", "gxms" })
			_root.GetNode<Control>(path + id).Hide();
		var warning = _root.GetNode<Label>(path + "warning");
		warning.Text = "出售背包内所有白装。\n已穿戴、有宝石及无售价的装备会保留。";
		warning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		warning.AddThemeFontSizeOverride("font_size", 16);
		_layout = _root.GetNode<MarginContainer>("MarginContainer");
		_layout.CustomMinimumSize = new(240, 0);
		_layout.Size = new(240, 0);
		_layout.Resized += () => _root.GetNode<ColorRect>("ColorRect").Size = _layout.Size;
		_root.GetNode<Control>("close").Position = new(210, 0);
		_root.GetNode<BaseButton>(path + "qd").Pressed += () =>
		{
			var result = events.ItemActionRequested.Send(new("sell_white_equipment", new(-1, "")), this);
			feedback(result.Message);
			if (result.Success) Hide();
		};
		_root.GetNode<BaseButton>("close").Pressed += Hide;
		_entry = parent.GetNode<BaseButton>("Main_Backpack/MarginContainer/VBoxContainer/HBoxContainer/pl_sell");
		_entry.Pressed += Show;
	}
	private void Show()
	{
		_root.Position = _parent.GetGlobalTransformWithCanvas().AffineInverse() * (_parent.GetViewportRect().Size / 2) - new Vector2(120, 140);
		_root.Show();
		Callable.From(() =>
		{
			_layout.Size = new(240, _layout.GetCombinedMinimumSize().Y);
			_root.GetNode<ColorRect>("ColorRect").Size = _layout.Size;
			_root.Size = _layout.Size;
			_root.Position = _parent.GetGlobalTransformWithCanvas().AffineInverse() * (_parent.GetViewportRect().Size / 2) - _layout.Size / 2;
		}).CallDeferred();
	}
	public void Hide() => _root.Hide();
	public void Dispose() { _entry.Pressed -= Show; _root.QueueFree(); }
}
