extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var terrain: Node2D = load("res://Scenes/Maps/Forest.tscn").instantiate()
	root.add_child(terrain)
	await physics_frame
	await physics_frame
	var space: PhysicsDirectSpaceState2D = terrain.get_world_2d().direct_space_state
	var flat_hit := space.intersect_ray(PhysicsRayQueryParameters2D.create(Vector2(700, 240), Vector2(700, 700), 1))
	if flat_hit.is_empty() or absf(flat_hit.position.y - 504.0) > 8.0:
		_fail("第一关平地无法定位刷怪地面")
		return
	var body := CapsuleShape2D.new()
	body.radius = 18
	body.height = 60
	var clearance := PhysicsShapeQueryParameters2D.new()
	clearance.shape = body
	clearance.transform = Transform2D(0, Vector2(700, flat_hit.position.y - 35))
	clearance.collision_mask = 1
	if not space.intersect_shape(clearance, 1).is_empty():
		_fail("第一关平地出生空间被墙体占据")
		return
	var slope_hit := space.intersect_ray(PhysicsRayQueryParameters2D.create(Vector2(1178, 240), Vector2(1178, 700), 1))
	if slope_hit.is_empty():
		_fail("第一关斜坡碰撞缺失")
		return
	if absf(flat_hit.position.y - slope_hit.position.y) <= 35.0:
		_fail("第一关坡顶没有形成应拒绝的高度差")
		return
	print("MONSTER_SPAWN_TERRAIN_SMOKE: PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("MONSTER_SPAWN_TERRAIN_SMOKE: " + message)
	quit(1)
