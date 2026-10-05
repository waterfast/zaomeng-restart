using System;
using Godot;

namespace Zaomeng.Level;

/// <summary>一局结束时的只读显示数据，不保存到玩家存档。</summary>
public sealed record LevelResult(bool Victory, string LevelName, DateTime StartedAt, DateTime EndedAt,
	double Duration, float Health, float MaxHealth, float Mana, float MaxMana, int MaximumCombo,
	int HitCount, int HurtCount, int CriticalCount, int DodgeCount, float[] DealtDamage, float[] ReceivedDamage, float Healing)
{
	// 沿用旧版评价门槛，用实际游玩总分钟计算，避免跨小时后重新获得高评价。
	public int Rating
	{
		get
		{
			int minutes = (int)(Duration / 60);
			int healthPercent = MaxHealth > 0 ? (int)(Health / MaxHealth * 100) : 0;
			if (minutes <= 2 && HurtCount == 0 && healthPercent >= 95) return 5;
			return minutes switch
			{
				<= 3 => healthPercent >= 85 ? 4 : healthPercent >= 75 ? 3 : healthPercent >= 65 ? 2 : 1,
				4 => healthPercent >= 85 ? 3 : healthPercent >= 65 ? 2 : 1,
				5 => healthPercent >= 75 ? 2 : 1,
				6 => healthPercent >= 85 ? 2 : 1,
				_ => 1
			};
		}
	}
}

/// <summary>只订阅当前玩家的战斗事实，结算后停止记录。</summary>
public sealed class LevelRunStatistics : IDisposable
{
	private readonly Player _player;
	private readonly DateTime _started = DateTime.Now;
	private readonly float[] _dealt = new float[3], _received = new float[3];
	private int _hits, _hurts, _criticals, _dodges;
	private float _healing;
	public double Duration { get; private set; }
	public LevelRunStatistics(Player player)
	{
		_player = player;
		player.DamageDealt += OnDamage;
		player.HitReceived += OnHurt;
		player.HealingReceived += OnHeal;
		player.AttackDodged += OnDodge;
	}
	public void Tick(double delta) => Duration += delta;
	private void OnDamage(HitResult hit) { _hits++; if (hit.Critical) _criticals++; _dealt[(int)hit.DamageType] += hit.Damage; }
	private void OnHurt(HitResult hit) { _hurts++; _received[(int)hit.DamageType] += hit.Damage; }
	private void OnHeal(float amount) => _healing += amount;
	private void OnDodge() => _dodges++;
	public LevelResult Finish(bool victory, string levelName, int maximumCombo) => new(victory, levelName, _started,
		DateTime.Now, Duration, _player.Health, _player.MaxHealth, _player.Mana, _player.MaxMana, maximumCombo,
		_hits, _hurts, _criticals, _dodges, (float[])_dealt.Clone(), (float[])_received.Clone(), _healing);
	public void Dispose()
	{
		_player.DamageDealt -= OnDamage;
		_player.HitReceived -= OnHurt;
		_player.HealingReceived -= OnHeal;
		_player.AttackDodged -= OnDodge;
	}
}
