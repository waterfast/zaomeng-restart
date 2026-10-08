using Godot;

namespace Zaomeng.Monsters;

/// <summary>场景显式配置特殊机制，不在通用怪物中添加种类分支。</summary>
public abstract partial class MonsterMechanic : Node2D
{
	protected Monster Actor => (Monster)GetParent();
	public abstract void ResetForSpawn();
}
