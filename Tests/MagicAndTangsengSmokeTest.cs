using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Zaomeng.Character;
using Zaomeng.Equipment;
using Zaomeng.Items;
using Zaomeng.Save;
using Zaomeng.Skills;
using Zaomeng.UI;
using Zaomeng.UI.Inventory;
using SaveCharacter = Zaomeng.Character.Character;

namespace Zaomeng;

/// <summary>内存角色验证，不读取或写入玩家存档。</summary>
public partial class MagicAndTangsengSmokeTest : Node2D
{
	private Player? _player;
	public override void _PhysicsProcess(double delta) => _player?._PhysicsProcess(delta);
	public override async void _Ready()
	{
		try
		{
			var items = GD.Load<ItemCatalog>("res://Content/Items/ItemCatalog.tres"); items.Validate();
			var character = new SaveCharacter { Id = "role_2", BaseStats = new() { MaxHealth = 10000, MaxMana = 10000, Attack = 200 } };
			var player = GD.Load<PackedScene>("res://Scenes/Actors/Tangseng.tscn").Instantiate<Player>();
			player.BindCharacter(character, items); AddChild(player);
			player.InputEnabled = false; player.Gravity = 0; player.Position = new(450, 430);
			player.SetPhysicsProcess(false);
			_player = player;
			var enemy = GD.Load<PackedScene>("res://Scenes/Actors/Monster.tscn").Instantiate<Monster>();
			enemy.MaxHealth = 100000; enemy.PhysicalDefense = 0; enemy.MagicDefense = 0;
			AddChild(enemy); enemy.AiEnabled = false; enemy.SetPhysicsProcess(false);
			var ally = GD.Load<PackedScene>("res://Scenes/Actors/Player.tscn").Instantiate<Player>();
			ally.MaxHealth = 100000; AddChild(ally); ally.InputEnabled = false; ally.SetPhysicsProcess(false);
			var hud = new CanvasLayer(); AddChild(hud);
			var menus = new MenuManager(); AddChild(menus);
			var bag = new Node2D { Name = "BackPack" }; hud.AddChild(bag); menus.RegisterMenu("bag", bag);
			using var view = new MagicWeaponView(hud, items, character);
			menus.RegisterMenu("artifacts_menu", view.Root); view.CloseRequested += menus.CloseMenu;
			menus.ToggleMenu("artifacts_menu");
			Check(view.Root.Visible && !bag.Visible && GetTree().Paused && view.Root.GetNode<Label>("BG/Name_").Text == "未装备法宝", "B opens independent empty detail and pauses");
			view.Root.GetNode<BaseButton>("BG/tips").EmitSignal(BaseButton.SignalName.Pressed);
			Check(hud.GetNode<Node2D>("MagicWeaponHelp").Visible, "help opens");
			menus.ToggleMenu("bag");
			Check(bag.Visible && !view.Root.Visible && !hud.GetNode<Node2D>("MagicWeaponHelp").Visible && GetTree().Paused, "switch to bag closes detail and help");
			menus.CloseMenu(); Check(!GetTree().Paused, "close restores pause state");
			foreach (string id in new[] { "dshl", "tsgj", "zjhl" })
			{
				character.Equipment.Set(EquipmentSlot.MagicWeapon, EquipmentInstance.Create(id, 0));
				player.RefreshCharacterStats(); player.RestoreMana(10000);
				view.Refresh(); menus.ToggleMenu("artifacts_menu");
				Check(view.Root.GetNode<Label>("BG/ScrollContainer/VBoxContainer/MagicWeaponSkill").Text.Contains("按 H"), $"{id} detail describes actual control");
				if (OS.GetCmdlineUserArgs().Contains("--capture-magic")) await Capture(id + "-detail");
				view.Root.GetNode<BaseButton>("BG/Close").EmitSignal(BaseButton.SignalName.Pressed);
				Check(!GetTree().Paused && !view.Root.Visible, "detail close restores gameplay");
				player.ResetForSpawn(new(450,430)); player.Face(1); player.SetPhysicsProcess(false);
				enemy.ResetForSpawn(new(id == "zjhl" ? 560 : 450,430));
				ally.ResetForSpawn(enemy.Position);
				float mana = player.Mana;
				Check(player.MagicWeapon.TryUse() && player.Mana == mana - player.MagicWeapon.Ability!.Action.GetManaCost(1), $"{id} spends mana once");
				Check(!player.MagicWeapon.TryUse(), "repeated cast rejected during cooldown");
				await Wait(1.2);
				Check(enemy.Health < enemy.MaxHealth && ally.Health == ally.MaxHealth, $"{id} damages enemy and preserves ally");
				if (OS.GetCmdlineUserArgs().Contains("--capture-magic")) await Capture(id);
			}
			character.Equipment.Set(EquipmentSlot.MagicWeapon, EquipmentInstance.Create("dshl",0));
			Check(player.MagicWeapon.Cooldown > 0 && !player.MagicWeapon.TryUse(), "changing away and back preserves cooldown");
			await Wait(3);
			player.TrySpendMana((int)player.Mana);
			Check(!player.MagicWeapon.TryUse(), "insufficient mana rejects cast");
			player.RestoreMana(10000);
			var learning = new SkillLearningService(character, new Wallet { Souls = 100000 });
			Check(!learning.TrySetKey(0, Key.H, out _), "magic hotkey cannot be assigned to character skill");
			foreach (string id in new[] { "xbz", "jhsj" })
			{
				Check(learning.TryLearn(id, out _) && learning.TryEquip(0,id,out _), $"{id} learns and equips from catalog"); learning.Apply(player);
				player.ResetForSpawn(new(450,430)); player.SetPhysicsProcess(false); player.Face(1);
				enemy.ResetForSpawn(new(id == "xbz" ? 530 : 450,430));
				var skill = learning.Catalog.Find(id)!.Action!;
				float mana = player.Mana;
				Check(player.TryUseSkill(skill) && player.Mana == mana - skill.GetManaCost(1), $"{id} casts original animation and mana cost");
				await Wait(1.2);
				Check(enemy.Health < enemy.MaxHealth, $"{id} deals real damage");
				if (OS.GetCmdlineUserArgs().Contains("--capture-magic")) await Capture(id);
				player.ResetForSpawn(player.Position);
				await Wait(skill.CooldownSeconds + .1);
				Check(player.TryUseSkill(skill), $"{id} recasts after cooldown");
				player.ReceiveHit(new HitResult(1,Vector2.Zero,.1f,DamageType.True));
				await Wait(.1);
				Check(!player.GetChildren().OfType<SkillBehavior>().Any(), $"{id} interrupted behavior released");
			}
			player.ResetForSpawn(player.Position);
			await Wait(7);
			Check(!GetChildren().OfType<AreaAttackEffect>().Any(), "all finite effects expire");
			Check(player.MagicWeapon.TryUse(), "magic recasts after cooldown");
			player.ResetForSpawn(player.Position);
			await Wait(.3);
			Check(!GetChildren().OfType<AreaAttackEffect>().Any(), "reset cancels unreleased magic sequence");
			await Wait(6.1);
			player.InputEnabled = true;
			Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.H, Keycode = Key.H, Pressed = true });
			float manaBeforeKey = player.Mana;
			await Wait(.08);
			Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.H, Keycode = Key.H, Pressed = false });
			Check(player.MagicWeapon.Cooldown > 0 && player.Mana < manaBeforeKey, "actual H input triggers equipped magic");
			player.InputEnabled = false;
			player.ReceiveHit(new HitResult(100000,Vector2.Zero,0,DamageType.True));
			await Wait(.3);
			Check(player.IsDead && !GetChildren().OfType<AreaAttackEffect>().Any() && !player.MagicWeapon.TryUse(), "death cancels pending magic and rejects casts");
			character.Equipment.Set(EquipmentSlot.MagicWeapon, null);
			Check(!player.MagicWeapon.TryUse(), "empty slot rejects cast");
			await Wait(GD.Load<AudioStream>("res://Assets/Audio/Hero/75_Role2_dead.mp3").GetLength() + .1);
			GD.Print("MAGIC AND TANGSENG SMOKE TEST PASSED"); GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task Capture(string name)
	{
		await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
		string directory = ProjectSettings.GlobalizePath("res://.godot/magic-checks"); System.IO.Directory.CreateDirectory(directory);
		GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory,name + ".png"));
	}
	private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); GD.Print("PASS: " + message); }
}
