using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Zaomeng.Level;

/// <summary>按资源运行固定波次，并在清波时解除下一路段的门禁。</summary>
public partial class WaveEncounter : Node
{
	private WaveEncounterDefinition _definition = null!;
	private float _spawnSpeed = 1;
	private WaveDefinition CurrentWave => _definition.Waves[Wave - 1];
	private readonly Dictionary<Monster, (Zaomeng.Monsters.MonsterDefinition Kind, float CorpseTime, bool Awarded)> _active = new();
	private readonly List<CollisionShape2D?> _gates = new();
	private Player _player = null!;
	private MonsterPool _pool = null!;
	private Node2D _world = null!;
	private int _spawned;
	private float _spawnClock = 1.5f;
	private bool _waveStarted;
	public int Wave { get; private set; } = 1;
	public int PendingCount => CurrentWave.Monsters.Count - _spawned;
	public int LivingCount { get; private set; }
	public bool IsCleared { get; private set; }
	public Monster? Boss { get; private set; }
	public bool NeedsAdvance => !IsCleared && Wave > 1 && !_waveStarted && _player.Position.X < CurrentWave.EntranceX;
	// 留出武器和人物图片超过胶囊的部分，贴墙时也能完整显示角色。
	public int CameraRight => CurrentWave.CameraRight;
	public event Action<Monster>? MonsterDefeated;
	public event Action? Cleared;
	public string Status => IsCleared ? $"{_levelName} 通关" : CurrentWave.Monsters.Any(monster => monster.IsBoss)
		? $"{_levelName} Boss" : !_waveStarted ? $"{_levelName} 第{Wave}波"
		: $"{_levelName} 第{Wave}波 · 剩余 {LivingCount + PendingCount}只";
	private string _levelName = "";

	public void Configure(Node2D world, Player player, MonsterPool pool, WaveEncounterDefinition definition, string levelName, float spawnSpeed)
	{
		_world = world;
		_player = player;
		_pool = pool;
		_definition = definition;
		_levelName = levelName;
		_spawnSpeed = spawnSpeed;
		var gateBody = new StaticBody2D { Name = "WaveGates", CollisionLayer = 1, CollisionMask = 0 };
		world.AddChild(gateBody);
		// 门禁只在进入路段前启用；清波后关闭，绝不在角色身上突然生成碰撞。
		foreach (WaveDefinition wave in _definition.Waves)
		{
			if (wave.GateX <= 0) { _gates.Add(null); continue; }
			float x = wave.GateX;
			var gate = new CollisionShape2D { Position = new(x, 200), Shape = new RectangleShape2D { Size = new(14, 800) } };
			gateBody.AddChild(gate);
			_gates.Add(gate);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player.IsDead) return;
		UpdateMonsters((float)delta);
		// 清场后仍要回收Boss尸体；等待玩家进入光圈不能留下常驻死亡画面。
		if (IsCleared) return;
		if (!_waveStarted)
		{
			if (_player.Position.X < CurrentWave.EntranceX) return;
			_waveStarted = true;
		}
		if (PendingCount == 0 && LivingCount == 0)
		{
			if (Wave == _definition.Waves.Count)
			{
				IsCleared = true;
				Cleared?.Invoke();
				return;
			}
			if (CurrentWave.ReleaseGateOnClear) _gates[Wave - 1]?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			Wave++;
			_spawned = 0;
			_spawnClock = 0;
			_waveStarted = false;
			return;
		}
		if (PendingCount <= 0 || LivingCount >= CurrentWave.LivingLimit) return;
		_spawnClock += (float)delta;
		if (_spawnClock < CurrentWave.SpawnInterval / _spawnSpeed) return;
		if (!TrySpawn(out Vector2 position)) return;
		_spawnClock = 0;
		var kind = CurrentWave.Monsters[_spawned++];
		Monster monster = _pool.Spawn(kind, position);
		monster.Summon = (summoned, count) => SummonMonsters(monster, summoned, count);
		_active.Add(monster, (kind, 0, false));
		LivingCount++;
		if (monster.IsBoss) Boss = monster;
	}
	private void SummonMonsters(Monster source, Zaomeng.Monsters.MonsterDefinition kind, int count)
	{
		if (source.IsDead || IsCleared || _player.IsDead) return;
		int capacity = Mathf.Max(0, 10 - _active.Keys.Count(m => !m.IsDead));
		for (int i = 0; i < Mathf.Min(count, capacity); i++)
		{
			float x = Mathf.Clamp(source.Position.X + (i % 2 == 0 ? -1 : 1) * (120 + i * 35),
				CurrentWave.EntranceX + 40, CurrentWave.GateX - 40);
			var query = PhysicsRayQueryParameters2D.Create(new(x, CurrentWave.SpawnRayTop), new(x, CurrentWave.SpawnRayBottom), 1);
			var ground = _world.GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (ground.Count == 0) continue;
			var actor = _pool.Spawn(kind, new(x, ground["position"].AsVector2().Y - 45));
			actor.GrantsRewards = false;
			_summonOwners[actor] = source;
			_active.Add(actor, (kind, 0, false));
			LivingCount++;
		}
	}

	private void UpdateMonsters(float delta)
	{
		foreach (var (summoned, owner) in new Dictionary<Monster, Monster>(_summonOwners))
			if (owner.IsDead && !summoned.IsDead)
			{
				summoned.Buffs.Clear();
				summoned.ReceiveHit(new(summoned.MaxHealth * 10, Vector2.Zero, 0, DamageType.True));
			}
		LivingCount = 0;
		foreach (var (monster, state) in new List<KeyValuePair<Monster, (Zaomeng.Monsters.MonsterDefinition Kind, float CorpseTime, bool Awarded)>>(_active))
		{
			if (!monster.IsDead) { LivingCount++; continue; }
			if (!state.Awarded) MonsterDefeated?.Invoke(monster);
			float corpseTime = state.CorpseTime + delta;
			if (corpseTime < 1) { _active[monster] = (state.Kind, corpseTime, true); continue; }
			_active.Remove(monster);
			_summonOwners.Remove(monster);
			_pool.Release(state.Kind, monster);
		}
	}
	private readonly Dictionary<Monster, Monster> _summonOwners = new();

	private bool TrySpawn(out Vector2 position)
	{
		Vector2 range = CurrentWave.SpawnRange;
		for (int attempt = 0; attempt < 10; attempt++)
		{
			float x = (float)GD.RandRange(range.X, range.Y);
			if (Mathf.Abs(x - _player.Position.X) < (CurrentWave.Monsters[_spawned].IsBoss ? 35 : 100)) continue;
			var query = PhysicsRayQueryParameters2D.Create(new(x, CurrentWave.SpawnRayTop), new(x, CurrentWave.SpawnRayBottom), 1);
			var ground = _world.GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (ground.Count == 0 || ground["normal"].AsVector2().Dot(Vector2.Up) < CurrentWave.MinimumGroundNormal) continue;
			position = new(x, ground["position"].AsVector2().Y - 2);
			return true;
		}
		position = default;
		return false;
	}
}
