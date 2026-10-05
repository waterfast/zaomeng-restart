using System;
using System.Collections.Generic;
using Godot;

namespace Zaomeng.Level;

/// <summary>按资源运行固定波次，并在清波时解除下一路段的门禁。</summary>
public partial class ForestEncounter : Node
{
	private WaveEncounterDefinition _definition = null!;
	private float _spawnSpeed = 1;
	private WaveDefinition CurrentWave => _definition.Waves[Wave - 1];
	private readonly Dictionary<Monster, (int Kind, float CorpseTime, bool Awarded)> _active = new();
	private readonly List<CollisionShape2D> _gates = new();
	private Player _player = null!;
	private MonsterPool _pool = null!;
	private Node2D _world = null!;
	private int _spawned;
	private float _spawnClock = 1.5f;
	private bool _waveStarted;
	public int Wave { get; private set; } = 1;
	public int PendingCount => CurrentWave.MonsterKinds.Length - _spawned;
	public int LivingCount { get; private set; }
	public bool IsCleared { get; private set; }
	public Monster? Boss { get; private set; }
	public bool NeedsAdvance => !IsCleared && Wave > 1 && Wave < _definition.Waves.Count && !_waveStarted;
	// 留出武器和人物图片超过胶囊的部分，贴墙时也能完整显示角色。
	public int CameraRight => CurrentWave.CameraRight;
	public event Action<Monster>? MonsterDefeated;
	public event Action? Cleared;
	public string Status => IsCleared ? $"{_levelName} 通关" : !_waveStarted && Wave < _definition.Waves.Count
		? $"{_levelName} 第{Wave}/{_definition.Waves.Count - 1}波" : Wave < _definition.Waves.Count
		? $"{_levelName} 第{Wave}/{_definition.Waves.Count - 1}波 · 剩余 {LivingCount + PendingCount}只"
		: $"{_levelName} Boss";
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
			if (wave.GateX <= 0) continue;
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
			if (Wave < _definition.Waves.Count - 1) _gates[Wave - 1].SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
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
		int kind = CurrentWave.MonsterKinds[_spawned++];
		Monster monster = _pool.Spawn(kind, position);
		_active.Add(monster, (kind, 0, false));
		LivingCount++;
		if (kind == 4) Boss = monster;
	}

	private void UpdateMonsters(float delta)
	{
		LivingCount = 0;
		foreach (var (monster, state) in new List<KeyValuePair<Monster, (int Kind, float CorpseTime, bool Awarded)>>(_active))
		{
			if (!monster.IsDead) { LivingCount++; continue; }
			if (!state.Awarded) MonsterDefeated?.Invoke(monster);
			float corpseTime = state.CorpseTime + delta;
			if (corpseTime < 1) { _active[monster] = (state.Kind, corpseTime, true); continue; }
			_active.Remove(monster);
			_pool.Release(state.Kind, monster);
		}
	}

	private bool TrySpawn(out Vector2 position)
	{
		Vector2 range = CurrentWave.SpawnRange;
		for (int attempt = 0; attempt < 10; attempt++)
		{
			float x = (float)GD.RandRange(range.X, range.Y);
			if (Mathf.Abs(x - _player.Position.X) < (Wave == _definition.Waves.Count ? 35 : 100)) continue;
			var query = PhysicsRayQueryParameters2D.Create(new(x, 200), new(x, 650), 1);
			var ground = _world.GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (ground.Count == 0 || ground["normal"].AsVector2().Dot(Vector2.Up) < 0.9f) continue;
			position = new(x, ground["position"].AsVector2().Y - 2);
			return true;
		}
		position = default;
		return false;
	}
}
