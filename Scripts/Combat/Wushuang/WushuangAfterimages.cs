using Godot;

namespace Zaomeng.Combat.Wushuang;

/// <summary>只复制当前可见纹理和变换，避免把角色脚本、动画和碰撞复制进残影。</summary>
public partial class WushuangAfterimages : Node
{
	private Player _owner = null!;
	private Node2D _visual = null!;
	private double _clock;
	public override void _Ready()
	{
		_owner = GetParent<Player>();
		_visual = _owner.GetNode<Node2D>("Facing/Visual");
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!_owner.Wushuang.IsActive || _owner.IsDead) { _clock = 0; return; }
		_clock += delta;
		if (_clock < 0.1) return;
		_clock %= 0.1;
		var ghost = new Node2D { Name = "WushuangAfterimage", ZIndex = _owner.ZIndex - 1,
			Modulate = new Color(1, 0.85098f, 0.827451f, 0.8f) };
		_owner.GetParent().AddChild(ghost);
		CopySprites(_visual, ghost);
		var tween = ghost.CreateTween();
		tween.TweenProperty(ghost, "modulate:a", 0f, 0.27);
		tween.TweenCallback(Callable.From(ghost.QueueFree));
	}
	private static void CopySprites(Node root, Node2D ghost)
	{
		for (int index = 0; index < root.GetChildCount(); index++)
		{
			Node child = root.GetChild(index);
			if (child is CanvasItem canvas && !canvas.IsVisibleInTree()) continue;
			Sprite2D? copy = child switch
			{
				Sprite2D sprite when sprite.Texture is not null => new Sprite2D {
					Texture = sprite.Texture, Centered = sprite.Centered, Offset = sprite.Offset,
					Hframes = sprite.Hframes, Vframes = sprite.Vframes, Frame = sprite.Frame,
					RegionEnabled = sprite.RegionEnabled, RegionRect = sprite.RegionRect,
					FlipH = sprite.FlipH, FlipV = sprite.FlipV },
				AnimatedSprite2D sprite when sprite.SpriteFrames is not null => new Sprite2D {
					Texture = sprite.SpriteFrames.GetFrameTexture(sprite.Animation, sprite.Frame),
					Centered = sprite.Centered, Offset = sprite.Offset, FlipH = sprite.FlipH, FlipV = sprite.FlipV },
				_ => null
			};
			if (copy is not null && child is Node2D source)
			{
				ghost.AddChild(copy);
				copy.GlobalTransform = source.GlobalTransform;
				copy.Modulate = source.Modulate * source.SelfModulate;
				copy.ZIndex = source.ZIndex;
			}
			CopySprites(child, ghost);
		}
	}
}
