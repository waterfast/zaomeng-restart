using System.Collections.Generic;
using Godot;
using Zaomeng.Monsters;
namespace Zaomeng.Level;
/// <summary>资源引用同时代表表现和属性变体，实例不共享战斗状态。</summary>
public sealed class MonsterPool(Node parent, LevelDifficultyDefinition? difficulty = null)
{
	private readonly Dictionary<MonsterDefinition, Stack<Monster>> _available = new();
	public Monster Spawn(MonsterDefinition definition, Vector2 position)
	{
		if (!_available.TryGetValue(definition, out var stack))
		{
			definition.Validate();
			_available[definition] = stack = new();
		}
		Monster monster;
		if (stack.Count > 0) monster = stack.Pop();
		else
		{
			monster = definition.ActorScene.Instantiate<Monster>();
			monster.TargetPath = new NodePath("../../Player");
			definition.Apply(monster, difficulty);
			parent.AddChild(monster);
		}
		// 每次从模板重新计算，不在已经放大的运行属性上重复乘难度。
		definition.Apply(monster, difficulty);
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
	public void Release(MonsterDefinition definition, Monster monster)
	{
		monster.CancelSkillBehavior();
		monster.AiEnabled = false;
		monster.GetNode<HitBox>("Facing/HitBox").Active = false;
		monster.CollisionLayer = 0;
		monster.CollisionMask = 0;
		monster.GetNode<HurtBox>("HurtBox").CollisionLayer = 0;
		monster.GetNode<HitBox>("Facing/HitBox").CollisionMask = 0;
		monster.Hide();
		monster.ProcessMode = Node.ProcessModeEnum.Disabled;
		_available[definition].Push(monster);
	}
}
