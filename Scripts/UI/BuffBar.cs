using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Zaomeng.UI;

/// <summary>只展示角色的活动状态；名称和说明直接来自 Buff 配置。</summary>
public partial class BuffBar : HBoxContainer
{
	private Player _player = null!;
	private float _clock;
	private readonly Dictionary<string, TextureRect> _entries = new();
	public void Bind(Player player) => _player = player;
	public override void _Process(double delta)
	{
		_clock -= (float)delta;
		if (_clock > 0 || _player is null) return;
		_clock = 0.2f;
		var active = _player.Buffs.Active;
		foreach (var key in _entries.Keys.Except(active.Select(b => b.Definition.Id)).ToArray())
		{
			RemoveChild(_entries[key]);
			_entries[key].QueueFree();
			_entries.Remove(key);
		}
		foreach (var buff in active)
		{
			if (_entries.TryGetValue(buff.Definition.Id, out var entry))
			{
				entry.TooltipText = Describe(buff.Definition, buff.Remaining);
				continue;
			}
			var icon = new TextureRect { Texture = buff.Definition.Icon ?? GD.Load<Texture2D>("res://Assets/Art/Skill/default_black.svg"),
				CustomMinimumSize = new(35, 35), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TooltipText = Describe(buff.Definition, buff.Remaining) };
			AddChild(icon);
			_entries.Add(buff.Definition.Id, icon);
		}
	}

	private static string Describe(Zaomeng.Combat.Buffs.BuffDefinition definition, float remaining) =>
		definition.DisplayName + "\n" + definition.Description +
		(float.IsPositiveInfinity(remaining) ? "" : $"\n剩余 {Mathf.CeilToInt(remaining)} 秒");
}
