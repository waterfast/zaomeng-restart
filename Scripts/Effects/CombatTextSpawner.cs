using Godot;

namespace Zaomeng;

/// <summary>战斗显示只读取结算结果，不参与伤害或玩家成长计算。</summary>
public static class CombatTextSpawner
{
	public static void ShowDamage(CharacterActor target, HitResult hit)
	{
		if (!target.IsInsideTree() || hit.Damage <= 0) return;
		var effect = GD.Load<PackedScene>("res://Scenes/Effects/DamageText.tscn").Instantiate<LegacyDamageText>();
		effect.Configure(hit, target is Player);
		AddInWorld(target, effect);
	}

	public static void ShowMiss(CharacterActor target)
	{
		if (!target.IsInsideTree()) return;
		AddInWorld(target, GD.Load<PackedScene>("res://Scenes/Effects/MissEffect.tscn").Instantiate<Node2D>());
	}
	public static void ShowLevelUp(Player player) => Show(player, "升级！", new Color("8dff8a"), true);

	private static void AddInWorld(CharacterActor target, Node2D effect)
	{
		effect.Name = "CombatText";
		effect.ZIndex = 100;
		target.GetParent().AddChild(effect);
		effect.GlobalPosition = target.GlobalPosition + new Vector2(-20, -80);
	}

	private static void Show(CharacterActor target, string text, Color color, bool emphasized = false)
	{
		if (!target.IsInsideTree()) return;
		// 放到世界分支，不随角色镜像或尸体池回收；暂停时随战斗一起停住。
		var root = new Node2D { Name = "CombatText", ZIndex = 100 };
		target.GetParent().AddChild(root);
		root.GlobalPosition = target.GlobalPosition + new Vector2(GD.RandRange(-14, 14), -80);
		var label = new Label
		{
			Text = text, Position = new Vector2(-90, -18), Size = new Vector2(180, 36),
			HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Font/8_FZCuYuan-M03S.ttf"));
		label.AddThemeFontSizeOverride("font_size", emphasized ? 25 : 20);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 5);
		root.AddChild(label);
		// 参考旧 DamageText：0.1 秒放大，0.2 秒恢复，0.6 秒上浮 50，1 秒消失。
		Tween rise = root.CreateTween();
		rise.TweenProperty(root, "position", root.Position + new Vector2(0, -50), 0.6);
		Tween pulse = root.CreateTween();
		pulse.TweenProperty(root, "scale", Vector2.One * (emphasized ? 2.2f : 1.6f), 0.1);
		pulse.TweenProperty(root, "scale", Vector2.One, 0.1);
		Tween fade = root.CreateTween();
		fade.TweenInterval(0.6);
		fade.TweenProperty(root, "modulate:a", 0f, 0.4);
		fade.TweenCallback(Callable.From(root.QueueFree));
	}
}
