using System.Collections.Generic;
using Godot;

namespace Zaomeng.Level;

/// <summary>按怪物种类复用运行时节点；退场时关闭物理参与，避免尸体占用碰撞。</summary>
public sealed class MonsterPool(Node parent)
{
	private readonly Dictionary<int, Stack<Monster>> _available = new();
	private static readonly string[] ScenePaths =
	{
		"", "res://Scenes/Actors/Monster.tscn", "res://Scenes/Actors/Monster2.tscn", "res://Scenes/Actors/Monster3.tscn", "res://Scenes/Actors/ForestBoss.tscn"
	};

	public Monster Spawn(int kind, Vector2 position)
	{
		if (!_available.TryGetValue(kind, out Stack<Monster>? stack))
			_available[kind] = stack = new Stack<Monster>();
		Monster monster;
		if (stack.Count > 0) monster = stack.Pop();
		else
		{
			monster = GD.Load<PackedScene>(ScenePaths[kind]).Instantiate<Monster>();
			monster.TargetPath = new NodePath("../../Player");
			parent.AddChild(monster);
		}
		monster.ProcessMode = Node.ProcessModeEnum.Inherit;
		monster.CollisionLayer = 2;
		monster.CollisionMask = 1;
		monster.GetNode<HurtBox>("HurtBox").CollisionLayer = 4;
		monster.GetNode<HitBox>("Facing/HitBox").CollisionMask = 4;
		monster.ResetForSpawn(position);
		monster.AiEnabled = true;
		monster.Show();
		return monster;
	}

	public void Release(int kind, Monster monster)
	{
		monster.AiEnabled = false;
		monster.GetNode<HitBox>("Facing/HitBox").Active = false;
		monster.CollisionLayer = 0;
		monster.CollisionMask = 0;
		monster.GetNode<HurtBox>("HurtBox").CollisionLayer = 0;
		monster.GetNode<HitBox>("Facing/HitBox").CollisionMask = 0;
		monster.Hide();
		monster.ProcessMode = Node.ProcessModeEnum.Disabled;
		_available[kind].Push(monster);
	}
}
