using System;
using System.Linq;
using Godot;
using Zaomeng.Level;

namespace Zaomeng.UI;

/// <summary>只显示本次结果并发出导航请求，奖励与进度提交由关卡负责。</summary>
public partial class LevelSettlement : CanvasLayer
{
	public event Action? ReturnRequested;
	public event Action? RetryRequested;
	public LevelResult Result { get; private set; } = null!;
	private Node2D _view = null!;
	public void ShowResult(LevelResult result)
	{
		Result = result;
		Layer = 20;
		ProcessMode = ProcessModeEnum.Always;
		string scene = result.Victory ? "Victory" : "Defeat";
		_view = GD.Load<PackedScene>($"res://Scenes/UI/Settlement/{scene}.tscn").Instantiate<Node2D>();
		AddChild(_view);
		_view.Position = Vector2.Zero;
		_view.ProcessMode = ProcessModeEnum.Always;
		_view.Scale = GetViewport().GetVisibleRect().Size / new Vector2(960, 540);
		var background = _view.GetNode<Sprite2D>("bg");
		background.Position = new(480, 270);
		background.Scale = new Vector2(960, 540) / background.Texture.GetSize();
		_view.GetNode<BaseButton>("return_map").Pressed += () => ReturnRequested?.Invoke();
		_view.GetNode<BaseButton>(result.Victory ? "Rechallenge" : "ReChallenge").Pressed += () => RetryRequested?.Invoke();
		if (result.Victory)
		{
			_view.GetNode<Label>("GameUseTime/BeginTime").Text = $"开始时间：{result.StartedAt:HH:mm:ss}";
			_view.GetNode<Label>("GameUseTime2/EndTime").Text = $"结束时间：{result.EndedAt:HH:mm:ss}";
			_view.GetNode<Label>("GameUseTime3/UseTIME").Text = $"过关用时：{TimeSpan.FromSeconds(result.Duration):mm\\:ss}";
			_view.GetNode<Label>("PlayerLastHp/Hp_text").Text = $"剩余状态：HP {result.Health:0}/{result.MaxHealth:0}  MP {result.Mana:0}/{result.MaxMana:0}";
			_view.GetNode<Label>("MostLj/MostLj_text").Text = $"最高连击：{result.MaximumCombo}";
			_view.GetNode<BaseButton>("MoreInformaition").Pressed += ShowDetails;
			var animation = _view.GetNode<AnimationPlayer>("GradesSHow");
			animation.ProcessMode = ProcessModeEnum.Always;
			animation.Play("Show");
			animation.AnimationFinished += _ => ShowRating();
		}
		else
		{
			_view.GetNode<Control>("ReChallenge").Position = new(165, 450);
			_view.GetNode<Control>("return_map").Position = new(400, 450);
			var summary = new Label { Position = new(240, 380), Size = new(480, 40), HorizontalAlignment = HorizontalAlignment.Center,
				Text = $"{result.LevelName}   用时 {TimeSpan.FromSeconds(result.Duration):mm\\:ss}   最高连击 {result.MaximumCombo}" };
			summary.AddThemeFontOverride("font", GD.Load<FontFile>("res://Assets/Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf"));
			summary.AddThemeFontSizeOverride("font_size", 24);
			_view.AddChild(summary);
			var details = new Button { Name = "Details", Text = "战斗详情", Position = new(635, 450), Size = new(180, 60) };
			details.AddThemeFontOverride("font", GD.Load<FontFile>("res://Assets/Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf"));
			details.Pressed += ShowDetails;
			_view.AddChild(details);
		}
	}
	private void ShowRating()
	{
		var rating = GD.Load<PackedScene>("res://Scenes/UI/Settlement/Rating.tscn").Instantiate<Node2D>();
		rating.Name = "Rating";
		rating.Position = new(65, 65);
		_view.AddChild(rating);
		rating.GetNode<TextureRect>("TextureRect").Texture = GD.Load<Texture2D>($"res://Assets/Art/Level/Challenge/ui_beizhan_pj_{Result.Rating}.png");
		rating.GetNode<AnimationPlayer>("AnimationPlayer").Play("show");
	}
	private void ShowDetails()
	{
		if (HasNode("Details")) return;
		var details = GD.Load<PackedScene>("res://Scenes/UI/Settlement/Details.tscn").Instantiate<Node2D>();
		details.Name = "Details";
		details.Scale = _view.Scale;
		AddChild(details);
		string left = "HBoxContainer/VBoxContainer/", right = "HBoxContainer/VBoxContainer2/";
		void Set(string path, string text) => details.GetNode<Label>(path).Text = text;
		Set(left + "TotalHitCount", $"累计命中次数：{Result.HitCount}");
		Set(left + "TotalHit", $"累计造成伤害：{Result.DealtDamage.Sum():0}");
		Set(left + "Totalphyhit", $"累计造成物伤：{Result.DealtDamage[0]:0}");
		Set(left + "Totalmaghit", $"累计造成魔伤：{Result.DealtDamage[1]:0}");
		Set(left + "TotalRealHit", $"累计造成真伤：{Result.DealtDamage[2]:0}");
		Set(left + "TotalCritCount", $"累计暴击次数：{Result.CriticalCount}");
		Set(right + "TotalHurtCount", $"累计受伤次数：{Result.HurtCount}");
		Set(right + "TotalHurt", $"累计受到伤害：{Result.ReceivedDamage.Sum():0}");
		Set(right + "TotalPhyHurt", $"累计受到物伤：{Result.ReceivedDamage[0]:0}");
		Set(right + "TotalMagHurt", $"累计受到魔伤：{Result.ReceivedDamage[1]:0}");
		Set(right + "TotalRealHurt", $"累计受到真伤：{Result.ReceivedDamage[2]:0}");
		Set(right + "TotalMissCount", $"累计闪避次数：{Result.DodgeCount}");
		Set("HBoxContainer/VBoxContainer3/TotalCure", $"累计治疗：{Result.Healing:0}");
		details.GetNode<BaseButton>("Glose").Pressed += details.QueueFree;
	}
}
