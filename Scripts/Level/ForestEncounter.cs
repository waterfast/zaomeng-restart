using System;
using System.Collections.Generic;
using Godot;

namespace Zaomeng.Level;

/// <summary>花果山原版固定队列：前三波9/14/15只，第四波5只，最后单独挑战大猩猩。</summary>
public partial class ForestEncounter : Node
{
	private static readonly int[][] Waves =
	[
		[1,1,1,1,1,1,1,1,1],
		[2,2,2,1,1,2,2,1,1,2,2,2,2,2],
		[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2],
		[2,2,2,2,2],
		[4]
	];
	private static readonly float[] Entrances = [0, 1700, 2760, 3610, 3610];
	public const float FinalBoundaryX = 4000;
	private static readonly Vector2[] SpawnRanges = [new(450, 900), new(2000, 2490), new(2880, 3320), new(3720, 3940), new(3780, 3930)];
	private readonly Dictionary<Monster, (int Kind, float CorpseTime, bool Awarded)> _active = new();
	private readonly List<CollisionShape2D> _gates = new();
	private Player _player = null!;
	private MonsterPool _pool = null!;
	private Node2D _world = null!;
	private int _spawned;
	private float _spawnClock = 1.5f;
	private bool _waveStarted;
	public int Wave { get; private set; } = 1;
	public int PendingCount => Waves[Wave - 1].Length - _spawned;
	public int LivingCount { get; private set; }
	public bool IsCleared { get; private set; }
	public Monster? Boss { get; private set; }
	public bool NeedsAdvance => !IsCleared && Wave is > 1 and <= 4 && !_waveStarted;
	// 留出武器和人物图片超过胶囊的部分，贴墙时也能完整显示角色。
	public int CameraRight => 40 + (Wave switch { 1 => 1661, 2 => 2723, 3 => 3563, _ => (int)FinalBoundaryX });
	public event Action<Monster>? MonsterDefeated;
	public event Action? Cleared;
	public string Status => IsCleared ? "花果山 通关" : !_waveStarted && Wave <= 4 ? $"花果山 第{Wave}/4波" : Wave <= 4
		? $"花果山 第{Wave}/4波 · 剩余 {LivingCount + PendingCount}只"
		: "花果山 Boss · 大猩猩";

	public void Configure(Node2D world, Player player, MonsterPool pool)
	{
		_world = world;
		_player = player;
		_pool = pool;
		var gateBody = new StaticBody2D { Name = "WaveGates", CollisionLayer = 1, CollisionMask = 0 };
		world.AddChild(gateBody);
		// 门禁只在进入路段前启用；清波后关闭，绝不在角色身上突然生成碰撞。
		foreach (float x in new[] { 1668f, 2730f, 3570f, FinalBoundaryX + 7 })
		{
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
			if (_player.Position.X < Entrances[Wave - 1]) return;
			_waveStarted = true;
		}
		if (PendingCount == 0 && LivingCount == 0)
		{
			if (Wave == 5)
			{
				IsCleared = true;
				Cleared?.Invoke();
				return;
			}
			if (Wave <= 3) _gates[Wave - 1].SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			Wave++;
			_spawned = 0;
			_spawnClock = 0;
			_waveStarted = false;
			return;
		}
		if (PendingCount <= 0 || LivingCount >= 5) return;
		_spawnClock += (float)delta;
		if (_spawnClock < 1.5f) return;
		if (!TrySpawn(out Vector2 position)) return;
		_spawnClock = 0;
		int kind = Waves[Wave - 1][_spawned++];
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
		Vector2 range = SpawnRanges[Wave - 1];
		for (int attempt = 0; attempt < 10; attempt++)
		{
			float x = (float)GD.RandRange(range.X, range.Y);
			if (Mathf.Abs(x - _player.Position.X) < (Wave == 5 ? 35 : 100)) continue;
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
