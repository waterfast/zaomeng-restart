using System;
using System.Linq;
using Godot;
using Zaomeng.Combat.Buffs;
using Zaomeng.Items;
using Zaomeng.Level;
using Zaomeng.Monsters;

namespace Zaomeng;

public partial class BuffAndSixLevelsSmokeTest : Node2D
{
	public override async void _Ready()
	{
		try
		{
			var catalog = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres");
			catalog.Validate();
			foreach (var entrance in GD.Load<UI.MainMenu.WorldMapDefinition>("res://Content/Maps/Human.tres").LevelEntrances)
				foreach (var current in entrance.Levels)
				{
					current.Validate(catalog);
					foreach (var itemId in current.GetPossibleDropIds(current.Difficulties[0]))
					{
						catalog.TryGetDefinition(itemId, out var item);
						if (item is Equipment.EquipmentDefinition equipment)
							Check(equipment.RequiredCharacterId is "" or "role_1" or "role_2", "所有地图掉落限定悟空唐僧与通用装备");
					}
				}
			for (int number = 4; number <= 9; number++)
			{
				var level = GD.Load<LevelDefinition>($"res://Content/Levels/human_{number}.tres");
				level.Validate(catalog);
				Check(!level.MapOnly && level.WaveEncounter!.Waves.Count == 4, "六关均有四波遭遇");
				Check(level.WaveEncounter!.Waves.Last().Monsters.Any(m => m.IsBoss), $"{number} 最后一波有首领");
				foreach (string id in level.GetPossibleDropIds(level.Difficulties[0]))
				{
					catalog.TryGetDefinition(id, out var item);
					if (item is Equipment.EquipmentDefinition equipment)
						Check(equipment.RequiredCharacterId is "" or "role_1" or "role_2", "掉落不含其他职业");
				}
				GD.Print($"PASS SIX CONTENT {number}: {level.DisplayName}");
			}
			var actor = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			actor.MaxHealth = 1000;
			AddChild(actor);
			actor.InputEnabled = false;
			actor.SetPhysicsProcess(false);
			var spirit = GD.Load<BuffDefinition>("res://Content/Buffs/earth_spirit.tres");
			actor.Buffs.Apply(spirit, "equipment", permanent: true);
			actor.Buffs.Apply(spirit, "other", permanent: true);
			Check(Mathf.IsEqualApprox(actor.Buffs.ExperienceMultiplier, 1.2f), "同ID不同来源不叠加");
			Check(Enumerable.Range(0, 5).Sum(_ => actor.Buffs.ScaleExperience(1)) == 6, "五次1经验累计为6");
			Check(Enumerable.Range(0, 5).Sum(_ => actor.Buffs.ScaleSouls(3)) == 18, "五次3灵魂累计为18");
			actor.Buffs.RemoveSource("equipment");
			Check(actor.Buffs.Active.Count == 1, "移除一个来源保留另一个");
			actor.Buffs.RemoveSource("other");
			Check(actor.Buffs.ExperienceMultiplier == 1, "所有来源移除恢复奖励");
			catalog.TryGetDefinition("dslj", out var ringItem);
			var ring = (Equipment.EquipmentDefinition)ringItem!;
			Check(ring.HasteBonus == 10, "地煞灵戒增加十极速");
			actor.PassiveEffects.SetEquipment(ring.GrantedSkills);
			Check(Mathf.IsEqualApprox(actor.Buffs.ExperienceMultiplier, 1.2f), "穿戴授予地煞之灵");
			var hud = GD.Load<PackedScene>("res://Scenes/UI/Level/Role_information.tscn").Instantiate<Node2D>();
			AddChild(hud);
			var binding = new UI.PlayerHud();
			hud.AddChild(binding);
			binding.Bind(hud, actor);
			var bar = hud.GetNode<UI.BuffBar>("roleLayer/role_hp_mp_exp/Buff_box/ActiveBuffs");
			bar._Process(0.2);
			Check(bar.GetChildren().Count == 1 && bar.GetChildren().All(child => child is TextureRect), "Buff仅显示图标，不生成装备或倒计时白字");
			Check(bar.GetGlobalTransformWithCanvas().Origin.IsEqualApprox(new Vector2(48, 74)), "Buff位于旧版角色框下方原始位置");
			if (OS.GetCmdlineUserArgs().Contains("--capture-buffs"))
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/buff-hud-review.png");
			}
			hud.QueueFree();
			actor.PassiveEffects.SetEquipment([]);
			Check(actor.Buffs.Active.Count == 0, "卸下装备移除地煞之灵");
			var food = new ConsumableDefinition { Id = "test_food", Effect = ConsumableEffect.ApplyBuff, Buff = new BuffDefinition { Id = "test_healing", DisplayName = "测试治疗", Duration = 10, HealRatioPerPulse = 0.02f } };
			var foodCatalog = new ItemCatalog { Definitions = new() { food } };
			var inventory = new Inventory.InventoryService(1, foodCatalog);
			inventory.RestoreSlots([new Inventory.ItemStack(food.Id, 2)]);
			var context = new ItemActionContext(new(0, food.Id), inventory.Slots[0]!, food, new Character.Character { Id = "role_1" });
			var wallet = new Save.Wallet();
			Check(!new ConsumableService(inventory, foodCatalog, wallet).Execute(context).Success && inventory.Slots[0]!.Count == 2, "无局内玩家不能吞掉食品");
			Check(new ConsumableService(inventory, foodCatalog, wallet, () => actor).Execute(context).Success && inventory.Slots[0]!.Count == 1, "食品共用Buff入口并消耗一件");
			actor.Buffs.Clear();
			var healing = new BuffDefinition { Id = "test_healing", DisplayName = "测试治疗", Duration = 10, HealRatioPerPulse = 0.02f };
			actor.ReceiveHit(new(500, Vector2.Zero, 0, DamageType.True));
			actor.Buffs.Apply(healing, "skill");
			actor.Buffs.Tick(5);
			Check(Mathf.IsEqualApprox(actor.Health, 600), "五秒五次治疗");
			actor.Buffs.Apply(healing, "skill");
			Check(actor.Buffs.Active.Single().Remaining == 10, "重施刷新不叠加");
			actor.Buffs.Tick(10);
			Check(actor.Buffs.Active.Count == 0 && Mathf.IsEqualApprox(actor.Health, 800), "到期结算最后一跳并移除");
			actor.Buffs.Apply(healing, "skill");
			actor.ReceiveHit(new(2000, Vector2.Zero, 0, DamageType.True));
			Check(actor.Buffs.Active.Count == 0, "死亡清理增益");
			GD.Print("BUFF AND SIX LEVELS SMOKE TEST PASSED");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void Check(bool value, string message)
	{
		if (!value) throw new InvalidOperationException(message);
	}
}
