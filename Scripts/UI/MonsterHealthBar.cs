using Godot;
using Zaomeng.Settings;

namespace Zaomeng.UI;

/// <summary>原版头顶细红血条；数值由演员持有，池复用时重置滞后条。</summary>
public partial class MonsterHealthBar : Node2D
{
	private CharacterActor _actor = null!;
	private TextureProgressBar _bar = null!;
	private TextureProgressBar _delayed = null!;
	private int _spawnRevision = -1;
	public void Bind(CharacterActor actor) => _actor = actor;
	public override void _Ready()
	{
		_bar = GetNode<TextureProgressBar>("blood_bar");
		_delayed = GetNode<TextureProgressBar>("blood_bar/blood_bar2");
	}

	public override void _Process(double delta)
	{
		Visible = !_actor.IsDead && GameSettings.IsEnabled(GameOption.MonsterHealthBar);
		float ratio = _actor.MaxHealth > 0 ? Mathf.Clamp(_actor.Health / _actor.MaxHealth, 0, 1) : 0;
		_bar.Value = ratio;
		bool smooth = GameSettings.IsEnabled(GameOption.SmoothHealthBars);
		_delayed.Visible = smooth;
		if (_spawnRevision != _actor.SpawnRevision || ratio >= _delayed.Value || !smooth)
			_delayed.Value = ratio;
		else _delayed.Value = Mathf.MoveToward((float)_delayed.Value, ratio, (float)delta * 2);
		_spawnRevision = _actor.SpawnRevision;
	}
}
