using Godot;

namespace Zaomeng.Level;

/// <summary>致死命中产生经验和原版灵魂光球；货币到达玩家后才交给关卡入账。</summary>
public static class MonsterRewardSpawner
{
	public static void Award(Monster monster, Player player)
	{
		if (!monster.GrantsRewards) return;
		player.GainExperience(player.Buffs.ScaleExperience(monster.ExperienceReward));
		if (monster.SoulValuePerOrb <= 0 || !monster.IsInsideTree()) return;
		Node parent = monster.GetParent();
		// 三颗光球一起快照并分配，避免每颗单独截断使小额灵魂增益失效。
		int souls = player.Buffs.ScaleSouls((int)System.Math.Min(int.MaxValue, (long)monster.SoulValuePerOrb * 3));
		for (int i = 0; i < 3; i++)
		{
			var orb = GD.Load<PackedScene>("res://Scenes/Effects/SoulPickup.tscn").Instantiate<SoulPickup>();
			Vector2 position = monster.GlobalPosition + new Vector2(GD.RandRange(-15, 15), GD.RandRange(-15, 15));
			orb.Position = parent is Node2D world ? world.ToLocal(position) : position;
			orb.Configure(player, souls / 3 + (i < souls % 3 ? 1 : 0));
			parent.AddChild(orb);
		}
	}
}
