using Godot;

namespace Zaomeng.UI;

/// <summary>实际命中才累计连击，按旧版100个物理帧的间隔重置；复用原数字和动画。</summary>
public partial class ComboHud : Node
{
	private const float ResetDelay = 100f / 60f;
	private Player _player = null!;
	private CanvasLayer _layer = null!;
	private Node2D? _popup;
	private float _remaining;
	public int Count { get; private set; }
	public int MaximumCount { get; private set; }
	public void Bind(Player player, CanvasLayer layer)
	{
		_player = player;
		_layer = layer;
		player.CombatHitConfirmed += OnHit;
	}
	public override void _ExitTree() => _player.CombatHitConfirmed -= OnHit;
	public override void _PhysicsProcess(double delta)
	{
		_remaining -= (float)delta;
		if (_remaining <= 0 || _player.IsDead) Count = 0;
		if (IsInstanceValid(_popup)) _popup!.Position = new(GetViewport().GetVisibleRect().Size.X - 205, 240);
	}
	private void OnHit()
	{
		Count++;
		MaximumCount = Mathf.Max(MaximumCount, Count);
		_remaining = ResetDelay;
		if (Count < 2) return;
		if (IsInstanceValid(_popup)) _popup!.QueueFree();
		_popup = GD.Load<PackedScene>("res://Scenes/UI/Level/Combo.tscn").Instantiate<Node2D>();
		_layer.AddChild(_popup);
		_popup.Position = new(GetViewport().GetVisibleRect().Size.X - 205, 240);
		var digits = _popup.GetNode<HBoxContainer>("ComBoxFather/ComboBox");
		foreach (char digit in Count.ToString())
			digits.AddChild(new TextureRect
			{
				Texture = GD.Load<Texture2D>($"res://Assets/Art/AllNumber/LJ/lj_{digit}.png"),
				StretchMode = TextureRect.StretchModeEnum.KeepCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
		_popup.GetNode<AnimationPlayer>("ComboPlayer").Play("Combo");
	}
}
