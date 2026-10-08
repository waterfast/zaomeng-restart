using Godot;
using Zaomeng.Combat.Effects;

namespace Zaomeng;

/// <summary>生成角色动作特效，并在指定时间后清理。</summary>
public static class ActorEffectSpawner
{
	public static void SpawnAttached(PackedScene scene, Node2D parent, Vector2 offset, float lifetime, AttackParameters? parameters = null)
	{
		Node2D effect = AddEffect(scene, parent, lifetime, parameters ?? AttackParameters.Default);
		effect.Position = offset;
	}

	public static void SpawnInWorld(PackedScene scene, Node parent, Vector2 globalPosition, float lifetime, AttackParameters? parameters = null)
	{
		Node2D effect = AddEffect(scene, parent, lifetime, parameters ?? AttackParameters.Default);
		effect.GlobalPosition = globalPosition;
	}

	private static Node2D AddEffect(PackedScene scene, Node parent, float lifetime, AttackParameters parameters)
	{
		// 特效场景以 Node2D 为根，才能设置相对位置或世界位置。
		Node2D effect = scene.Instantiate<Node2D>();
		parent.AddChild(effect);
		// 弹体在 Configure 中消费快照，不能再给根节点重复放大。
		if (effect is not Zaomeng.Skills.SkillProjectile)
		{
			effect.Scale *= parameters.VisualScale;
			ApplyPlaybackSpeed(effect, parameters.EffectSpeed);
		}

		// 时长为 0 时由特效场景自行销毁。
		if (lifetime > 0)
			parent.GetTree().CreateTimer(lifetime * parameters.EffectLifetime).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(effect)) effect.QueueFree();
			};

		return effect;
	}
	private static void ApplyPlaybackSpeed(Node node, float speed)
	{
		if (node is AnimatedSprite2D sprite) sprite.SpeedScale *= speed;
		if (node is AnimationPlayer animator) animator.SpeedScale *= speed;
		foreach (Node child in node.GetChildren()) ApplyPlaybackSpeed(child, speed);
	}
}
