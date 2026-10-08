using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng.Skills;

/// <summary>已启动序列的时钟；死亡和池复用立即取消未生成的后续爆发。</summary>
public partial class GroundAttackSequence : Node
{
	private CharacterActor _source = null!;
	private SkillDefinition _skill = null!;
	private GroundAttackSequenceBehavior _settings = null!;
	private Vector2 _point;
	private AttackParameters _parameters = AttackParameters.Default;
	private int _revision, _level, _next;
	private float _elapsed;
	public void Configure(CharacterActor source, SkillDefinition skill, int level,
		GroundAttackSequenceBehavior settings, Vector2 point, AttackParameters parameters)
	{
		_source = source; _skill = skill; _level = level; _revision = source.SpawnRevision;
		// 行为会随身体动作结束释放，复制配置节点避免持有已释放的 Godot 对象。
		_settings = (GroundAttackSequenceBehavior)settings.Duplicate();
		_point = point; _parameters = parameters;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_source) || _source.IsDead || !_source.Visible || _source.SpawnRevision != _revision
			|| (_next == 0 && _source.State == ActorState.Hurt))
		{ QueueFree(); return; }
		_elapsed += (float)delta * _parameters.EffectSpeed;
		while (_next < _settings.ReleaseTimes.Length && _elapsed >= _settings.ReleaseTimes[_next])
		{
			Vector2 point = _point + _settings.Offsets[_next++] * _parameters.Range;
			var query = PhysicsRayQueryParameters2D.Create(point + new Vector2(0, -200), point + new Vector2(0, 200), 1);
			var ground = _source.GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (ground.Count > 0) point.Y = ground["position"].AsVector2().Y;
			var effect = new AreaAttackEffect { VerticalRadius = _settings.Height,
				VisualMaterial = _settings.VisualMaterial, AlignVisualToGround = _settings.AlignVisualToGround };
			effect.Configure(_source, _skill, _level, _settings.Radius, _settings.Hits, _settings.Frames,
				_settings.Animation, _settings.VisualScale, _settings.VisualOffset, _parameters);
			GetParent().AddChild(effect);
			effect.GlobalPosition = point;
		}
		if (_next == _settings.ReleaseTimes.Length) QueueFree();
	}
	public override void _ExitTree() { if (IsInstanceValid(_settings)) _settings.Free(); }
}
