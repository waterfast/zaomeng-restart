using Godot;

namespace Zaomeng.Skills;

/// <summary>角色共用的地面范围打击；锁定目标脚点后预警，命中只结算敌对队伍。</summary>
public partial class AreaAttackBehavior : SkillBehavior
{
	[Export] public bool TargetEnemy { get; set; }
	[Export] public float Delay { get; set; } = 0.8f;
	[Export] public float Radius { get; set; } = 150;
	[Export] public Godot.Collections.Array<HitEvent> Hits { get; set; } = new();
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public StringName Animation { get; set; } = "impact";
	[Export] public Vector2 VisualScale { get; set; } = Vector2.One;
	[Export] public Vector2 VisualOffset { get; set; }
	private Vector2 _point;
	private Polygon2D? _warning;
	private bool _released;
	public override void ValidateConfiguration()
	{
		Require(float.IsFinite(Delay) && Delay > 0 && float.IsFinite(Radius) && Radius > 0
			&& Frames is not null && Frames.HasAnimation(Animation)
			&& Frames.GetFrameCount(Animation) > 0 && Frames.GetAnimationLoopMode(Animation) == SpriteFrames.LoopMode.None
			&& double.IsFinite(Frames.GetAnimationSpeed(Animation)) && Frames.GetAnimationSpeed(Animation) > 0
			&& VisualOffset.IsFinite() && VisualScale.IsFinite() && VisualScale.X > 0 && VisualScale.Y > 0, "范围技能配置无效。");
		int previousEnd = 0;
		Require(Hits.Count > 0, "范围技能缺少命中窗口。");
		foreach (var window in Hits)
		{
			Require(window?.Hit is not null && window.StartFrame >= previousEnd
				&& window.EndFrame > window.StartFrame && window.EndFrame <= Frames!.GetFrameCount(Animation), "范围技能命中窗口无效。");
			previousEnd = window!.EndFrame;
		}
	}
	protected override void OnBegin()
	{
		_point = Actor.GlobalPosition;
		if (TargetEnemy)
		{
			float nearest = float.MaxValue;
			foreach (Node node in Actor.GetTree().GetNodesInGroup("combat_actors"))
				if (node is CharacterActor { Visible: true, IsDead: false } target && target.Team != Actor.Team)
				{
					float distance = Actor.GlobalPosition.DistanceSquaredTo(target.GlobalPosition);
					if (distance >= nearest) continue;
					nearest = distance;
					_point = target.GlobalPosition;
				}
		}
		var query = PhysicsRayQueryParameters2D.Create(_point + new Vector2(0, -160), _point + new Vector2(0, 160), 1);
		var ground = Actor.GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (ground.Count > 0) _point = ground["position"].AsVector2();
		_warning = new Polygon2D { Color = new(1, 0.4f, 0.15f, 0.5f), ZIndex = 5,
			Polygon = [new(-Radius * Parameters.Range, -5), new(Radius * Parameters.Range, -5), new(Radius * Parameters.Range, 0), new(-Radius * Parameters.Range, 0)] };
		Actor.GetParent().AddChild(_warning);
		_warning.GlobalPosition = _point;
	}
	protected override void OnTick(float delta)
	{
		if (_released || Elapsed < Delay) return;
		_released = true;
		_warning?.QueueFree();
		_warning = null;
		var effect = new AreaAttackEffect();
		effect.Configure(Actor, Definition, Level, Radius, Hits, Frames, Animation, VisualScale, VisualOffset, Parameters);
		Actor.GetParent().AddChild(effect);
		// 脚点来自世界空间；关卡或测试容器有变换时也必须落在真实地面。
		effect.GlobalPosition = _point;
	}
	protected override void OnStop()
	{
		if (IsInstanceValid(_warning)) _warning!.QueueFree();
		_warning = null;
	}
}
