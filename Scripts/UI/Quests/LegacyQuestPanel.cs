using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zaomeng.Items;
using Zaomeng.Quests;

namespace Zaomeng.UI.Quests;

/// <summary>保留旧任务节点与图片，领取按钮只发送请求，收到完成通知后刷新。</summary>
public partial class LegacyQuestPanel : Node
{
	private Node2D _root = null!;
	private QuestService _service = null!;
	private QuestInteraction _interaction = null!;
	private ItemCatalog _items = null!;
	private QuestDefinition? _selected;
	private IDisposable? _changed;
	private readonly List<(QuestDefinition Quest, BaseButton Button)> _rows = new();
	public event Action? CloseRequested;
	public void Bind(Node2D root, QuestService service, QuestInteraction interaction, ItemCatalog items)
	{
		_root = root; _service = service; _interaction = interaction; _items = items;
		root.GetNode<BaseButton>("BG/Close").Pressed += () => CloseRequested?.Invoke();
		root.GetNode<BaseButton>("BG/TaskType/RcTask").Disabled = true;
		root.GetNode<BaseButton>("BG/TaskType/RcTask").TooltipText = "日常任务尚未迁入";
		root.GetNode<BaseButton>("BG/lqjl").Pressed += () =>
		{
			if (_selected is null) return;
			var result = _interaction.ClaimRequested.Send(new(_selected.Id));
			Refresh();
			_root.GetNode<Label>("BG/ms/ScrollContainer/Text").Text += "\n" + result.Message;
		};
		var list = root.GetNode<VBoxContainer>("BG/ScrollContainer/TaskList");
		foreach (var quest in service.Catalog.Definitions)
		{
			var button = GD.Load<PackedScene>("res://Scenes/UI/Task/TaskTitle.tscn").Instantiate<TextureButton>();
			list.AddChild(button);
			button.GetNode<Label>("Tilte").Text = quest.DisplayName;
			button.Pressed += () => { _selected = quest; Refresh(); };
			_rows.Add((quest, button));
		}
		_selected = service.Catalog.Definitions.FirstOrDefault();
		_changed = interaction.Changed.Subscribe(Refresh);
		root.VisibilityChanged += Refresh;
		Refresh();
	}
	public void Refresh()
	{
		_root.GetNode<Label>("BG/ColorRect/Title").Text = $"活动任务（{_rows.Count(value => _service.IsClaimed(value.Quest))}/{_rows.Count}）";
		_root.GetNode<Label>("BG/ColorRect/Title/Num").Hide();
		foreach (var (quest, button) in _rows)
			button.GetNode<Label>("Label").Text = _service.IsClaimed(quest) ? "已领取" : _service.CanClaim(quest) ? "可领取" : "进行中";
		var rewards = _root.GetNode<GridContainer>("BG/jl/ScrollContainer/RewardList");
		foreach (Node child in rewards.GetChildren()) { rewards.RemoveChild(child); child.QueueFree(); }
		_root.GetNode<BaseButton>("BG/lqjl").Disabled = _selected is null || !_service.CanClaim(_selected);
		if (_selected is null) { _root.GetNode<Label>("BG/ms/ScrollContainer/Text").Text = "暂无任务"; return; }
		_root.GetNode<Label>("BG/ms/ScrollContainer/Text").Text = _selected.Description +
			$"\n等级：{_service.Character.Level}/{_selected.RequiredLevel}" + (_service.IsClaimed(_selected) ? "\n已领取" : "");
		foreach (var reward in _selected.Rewards)
		{
			_items.TryGetDefinition(reward.ItemId, out var item);
			var view = GD.Load<PackedScene>("res://Scenes/UI/Task/TaskReward.tscn").Instantiate<TextureRect>();
			rewards.AddChild(view);
			view.GetNode<Button>("Items").Icon = item!.Icon;
			view.GetNode<Label>("Items/item_number").Text = reward.Count.ToString();
			view.GetNode<Label>("ScrollContainer/title").Text = item.DisplayName;
			view.TooltipText = item.Description;
		}
	}
	public override void _ExitTree() { _changed?.Dispose(); _root.VisibilityChanged -= Refresh; }
}
