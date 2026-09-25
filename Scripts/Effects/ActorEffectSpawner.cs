using Godot;

namespace Zaomeng;

/// <summary>生成角色动作特效，并在指定时间后清理。</summary>
public static class ActorEffectSpawner
{
    public static void SpawnAttached(PackedScene scene, Node2D parent, Vector2 offset, float lifetime)
    {
        Node2D effect = AddEffect(scene, parent, lifetime);
        effect.Position = offset;
    }

    public static void SpawnInWorld(PackedScene scene, Node parent, Vector2 globalPosition, float lifetime)
    {
        Node2D effect = AddEffect(scene, parent, lifetime);
        effect.GlobalPosition = globalPosition;
    }

    private static Node2D AddEffect(PackedScene scene, Node parent, float lifetime)
    {
        // 特效场景以 Node2D 为根，才能设置相对位置或世界位置。
        Node2D effect = scene.Instantiate<Node2D>();
        parent.AddChild(effect);

        // 时长为 0 时由特效场景自行销毁。
        if (lifetime > 0)
            parent.GetTree().CreateTimer(lifetime).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(effect)) effect.QueueFree();
            };

        return effect;
    }
}
