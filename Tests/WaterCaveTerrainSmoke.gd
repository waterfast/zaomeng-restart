extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var terrain: Node2D = load("res://Scenes/Maps/Human/Map_2.tscn").instantiate()
	root.add_child(terrain)
	await physics_frame
	await physics_frame
	var space := terrain.get_world_2d().direct_space_state
	# 从悬空平台下方发射，单独核对地面与水面，防止跨水域的隐形地基回归。
	for sample in [Vector2(800, 497), Vector2(1660, 497), Vector2(1800, 384),
		Vector2(1950, 560), Vector2(2200, 560), Vector2(2400, 560),
		Vector2(2700, 497), Vector2(3180, 560), Vector2(3300, 560),
		Vector2(3420, 560), Vector2(3900, 497), Vector2(4260, 425)]:
		var hit := space.intersect_ray(PhysicsRayQueryParameters2D.create(
			Vector2(sample.x, sample.y - 50), Vector2(sample.x, 700), 1))
		if hit.is_empty() or absf(hit.position.y - sample.y) > 0.5:
			_fail("地面高度错误：%s，命中 %s" % [sample, hit])
			return
	var body := CharacterBody2D.new()
	body.collision_layer = 0
	body.collision_mask = 1
	body.floor_snap_length = 6
	var capsule := CapsuleShape2D.new()
	capsule.radius = 18
	capsule.height = 60
	var collision := CollisionShape2D.new()
	collision.shape = capsule
	collision.position.y = -30
	body.add_child(collision)
	root.add_child(body)
	for water_x in [2100, 3300]:
		body.position = Vector2(water_x, 500)
		body.velocity = Vector2.ZERO
		for frame in range(90):
			await _step(body, 0)
		if not body.is_on_floor() or absf(body.position.y - 560) > 0.5:
			_fail("角色未贴水面站立：%s" % body.position)
			return
	# 两座坡都按双向连续行走验证，下降时也必须保持贴地。
	for route in [Vector2(300, 780), Vector2(4020, 4500)]:
		for direction in [1, -1]:
			body.position = Vector2(route.x if direction == 1 else route.y, 450)
			body.velocity = Vector2.ZERO
			for frame in range(60):
				await _step(body, 0)
			for frame in range(240):
				await _step(body, direction * 180)
				if body.position.x > route.x + 30 and body.position.x < route.y - 30:
					if not body.is_on_floor():
						_fail("坡道行走悬空：%s" % body.position)
						return
			if direction * (body.position.x - (route.y if direction == 1 else route.x)) < 0:
				_fail("坡道阻挡移动：%s" % body.position)
				return
	# 两个右岸均为真实台阶，用当前悟空的跳跃速度/重力验证上岸。
	for shore in [2438, 3458]:
		body.position = Vector2(shore - 65, 540)
		body.velocity = Vector2.ZERO
		for frame in range(45):
			await _step(body, 0)
		body.velocity.y = -430
		for frame in range(90):
			await _step(body, 180)
		if body.position.x < shore + 40 or not body.is_on_floor() or absf(body.position.y - 497) > 0.5:
			_fail("跳跃上岸失败：%s" % body.position)
			return
	print("WATER_CAVE_TERRAIN_SMOKE: PASS (水面贴地、双向坡道、跳跃上岸)")
	quit(0)


func _step(body: CharacterBody2D, horizontal: float) -> void:
	await physics_frame
	var vertical := 0.0 if body.is_on_floor() and body.velocity.y >= 0 else body.velocity.y + 600.0 / 60.0
	body.velocity = Vector2(horizontal, vertical)
	body.move_and_slide()


func _fail(message: String) -> void:
	push_error("WATER_CAVE_TERRAIN_SMOKE: " + message)
	quit(1)
