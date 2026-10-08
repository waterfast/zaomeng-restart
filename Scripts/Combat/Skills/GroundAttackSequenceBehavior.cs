using Godot;

namespace Zaomeng.Skills;

/// <summary>按资源配置在地面排列有限爆发，释放后独立于身体动作继续播放。</summary>
public partial class GroundAttackSequenceBehavior : SkillBehavior
{
	[Export] public float StartTime { get; set; }
	[Export] public float[] ReleaseTimes { get; set; } = [0];
	[Export] public Vector2[] Offsets { get; set; } = [Vector2.Zero];
	[Export] public bool TargetEnemy { get; set; }
	[Export] public bool SampleTargetOnRelease { get; set; }
	[Export] public float Radius { get; set; } = 45;
	[Export] public float Height { get; set; } = 140;
	[Export] public SpriteFrames Frames { get; set; } = null!;
	[Export] public StringName Animation { get; set; } = "";
	[Export] public Vector2 VisualScale { get; set; } = Vector2.One;
	[Export] public Vector2 VisualOffset { get; set; }
	[Export] public Material? VisualMaterial { get; set; }
	[Export] public bool AlignVisualToGround { get; set; }
	[Export] public Godot.Collections.Array<HitEvent> Hits { get; set; } = new();
	private Vector2 _point;
	private bool _started;

	public override void ValidateConfiguration()
	{
		Require(float.IsFinite(StartTime) && StartTime >= 0 && ReleaseTimes.Length > 0
			&& ReleaseTimes.Length == Offsets.Length && Radius > 0 && float.IsFinite(Radius)
			&& Height > 0 && float.IsFinite(Height) && VisualScale.IsFinite()
			&& VisualScale.X > 0 && VisualScale.Y > 0 && VisualOffset.IsFinite()
			&& Frames is not null && Frames.HasAnimation(Animation)
			&& Frames.GetFrameCount(Animation) > 0 && Frames.GetAnimationSpeed(Animation) > 0
			&& Frames.GetAnimationLoopMode(Animation) == SpriteFrames.LoopMode.None, "地面序列配置无效。");
		float previous = -1;
		for (int i = 0; i < ReleaseTimes.Length; i++)
		{
			Require(float.IsFinite(ReleaseTimes[i]) && ReleaseTimes[i] >= 0 && ReleaseTimes[i] >= previous
				&& Offsets[i].IsFinite(), "地面序列释放时间与偏移无效。");
			previous = ReleaseTimes[i];
		}
		Require(Hits.Count > 0, "地面序列没有命中窗口。");
		int end = 0;
		foreach (var hit in Hits)
		{
			Require(hit?.Hit is not null && hit.StartFrame >= end && hit.EndFrame > hit.StartFrame
				&& hit.EndFrame <= Frames!.GetFrameCount(Animation), "地面序列命中窗口无效。");
			hit!.Hit!.AppliedBuff?.Validate();
			end = hit.EndFrame;
		}
	}
	protected override void OnBegin() => _point = SamplePoint();
	private Vector2 SamplePoint()
	{
		Vector2 point = Actor.GlobalPosition;
		float nearest = float.MaxValue;
		if (TargetEnemy)
			foreach (Node node in Actor.GetTree().GetNodesInGroup("combat_actors"))
				if (node is CharacterActor { Visible: true, IsDead: false } target && target.Team != Actor.Team)
				{
					float distance = Actor.GlobalPosition.DistanceSquaredTo(target.GlobalPosition);
					if (distance >= nearest) continue;
					nearest = distance;
					point.X = target.GlobalPosition.X;
				}
		return point;
	}
	protected override void OnTick(float delta)
	{
		if (_started || Elapsed < StartTime) return;
		_started = true;
		if (SampleTargetOnRelease) _point = SamplePoint();
		var sequence = new GroundAttackSequence();
		sequence.Configure(Actor, Definition, Level, this, _point, Parameters);
		Actor.GetParent().AddChild(sequence);
	}
}
