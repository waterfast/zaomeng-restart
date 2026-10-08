extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var terrain: Node2D = load("res://Scenes/Maps/Human/Map_3.tscn").instantiate()
	root.add_child(terrain)
	await physics_frame
	await physics_frame
	var space := terrain.get_world_2d().direct_space_state
	# 包括旧人工坡道伸出的空地，避免“可走通”掩盖踩空气的碰撞回归。
	for sample in [Vector2(800, 515), Vector2(1100, 515), Vector2(1400, 367),
		Vector2(1800, 515), Vector2(2200, 515), Vector2(2400, 366.5),
		Vector2(2600, 223), Vector2(2900, 515), Vector2(3500, 515),
		Vector2(3600, 133), Vector2(3800, 322.25), Vector2(4000, 515),
		Vector2(3158, 238)]:
		var hit := space.intersect_ray(PhysicsRayQueryParameters2D.create(
			Vector2(sample.x, sample.y - 20), Vector2(sample.x, 900), 1))
		if hit.is_empty() or absf(hit.position.y - sample.y) > 0.5:
			push_error("桃花源地面/平台高度错误：%s，命中 %s" % [sample, hit])
			quit(1)
			return
	for node in terrain.get_node("wall").get_children():
		if not node is CollisionShape2D or not node.shape is RectangleShape2D:
			push_error("桃花源wall必须由可独立调整的矩形组成")
			quit(1)
			return
	print("PEACH GARDEN TERRAIN SMOKE PASS")
	quit()
