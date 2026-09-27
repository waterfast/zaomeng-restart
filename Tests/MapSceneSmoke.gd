extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var map_scene: PackedScene = load("res://Scenes/UI/MainMenu/Map1.tscn")
	if map_scene == null:
		_fail("第一张地图无法加载")
		return
	var map: Node2D = map_scene.instantiate()
	if map.get_node("background").texture == null:
		_fail("第一张地图的背景素材缺失")
		return
	for level in range(1, 4):
		if map.get_node_or_null("level_%d" % level) == null:
			_fail("第一张地图缺少第 %d 关入口" % level)
			return
	map.free()

	var choose: Node2D = load("res://Scenes/UI/MainMenu/ChoosePlayer.tscn").instantiate()
	root.add_child(choose)
	await process_frame
	var portrait: Button = choose.get_node("Bg/ScrollContainer/PlayerList").get_child(0)
	if portrait.icon == null:
		_fail("选角界面的孙悟空头像缺失")
		return
	print("MAP_SCENE_SMOKE: PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("MAP_SCENE_SMOKE: " + message)
	quit(1)
