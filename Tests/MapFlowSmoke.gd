extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	# 此测试会新建一号存档，只能在重定向后的独立用户目录运行。
	if not ProjectSettings.globalize_path("user://").contains("/.godot/test-appdata"):
		_fail("需要独立的用户数据目录")
		return
	var saves: Node2D = load("res://Scenes/UI/MainMenu/SaveSlots.tscn").instantiate()
	root.add_child(saves)
	current_scene = saves
	saves.get_node("ScrollContainer/AllAr").get_child(0).pressed.emit()
	await process_frame
	await process_frame
	if current_scene == null or current_scene.name != "ChoosePlayer":
		_fail("新存档没有进入选角界面")
		return
	current_scene.get_node("Bg/qued").pressed.emit()
	await process_frame
	await process_frame
	if current_scene == null or current_scene.name != "Map_1":
		_fail("选角后没有进入第一张地图")
		return
	if current_scene.get_node("level_1").disabled or not current_scene.get_node("level_2").disabled:
		_fail("新角色的地图解锁状态错误")
		return
	current_scene.get_node("level_1").pressed.emit()
	await process_frame
	await process_frame
	if current_scene == null or current_scene.name != "Level_1":
		_fail("选择第一关后没有进入关卡")
		return
	if current_scene.get_node("OldHud/roleLayer/role_hp_mp_exp/role_level").text != "1":
		_fail("新角色的局内等级不是 1")
		return
	current_scene.set("SpawnInterval", 0.05)
	for i in range(20):
		await physics_frame
	if current_scene.get_node("Enemies").get_child_count() == 0:
		_fail("第一关没有刷出小怪")
		return
	current_scene.get_node("Player").position.x = 4650
	await process_frame
	await process_frame
	if current_scene == null or current_scene.name != "Map_1" or current_scene.get_node("level_2").disabled:
		_fail("完成第一关后没有返回地图并解锁第二关")
		return
	current_scene.get_node("turn_mainmenu").pressed.emit()
	await process_frame
	await process_frame
	var loaded_slots: Node2D = load("res://Scenes/UI/MainMenu/SaveSlots.tscn").instantiate()
	current_scene.add_child(loaded_slots)
	loaded_slots.get_node("ScrollContainer/AllAr").get_child(0).pressed.emit()
	await process_frame
	await process_frame
	if current_scene == null or current_scene.name != "Map_1" or current_scene.get_node("level_2").disabled:
		_fail("读取已有存档没有恢复地图和第二关入口")
		return
	var delete_slots: Node2D = load("res://Scenes/UI/MainMenu/SaveSlots.tscn").instantiate()
	current_scene.add_child(delete_slots)
	var first_slot: Button = delete_slots.get_node("ScrollContainer/AllAr").get_child(0)
	if not first_slot.text.contains("孙悟空 Lv.1"):
		_fail("存档卡片没有显示角色和等级：" + first_slot.text)
		return
	delete_slots.get_node("background/cd_number").text = "1"
	delete_slots.get_node("background/delete").pressed.emit()
	var confirmation: ConfirmationDialog = delete_slots.find_children("*", "ConfirmationDialog", true, false)[0]
	confirmation.confirmed.emit()
	if FileAccess.file_exists("user://saves/save_01.json") or FileAccess.file_exists("user://saves/save_01.json.bak"):
		_fail("删除存档后主文件或备份仍然存在")
		return
	if not first_slot.is_queued_for_deletion() or not delete_slots.get_node("ScrollContainer/AllAr").get_child(0).text.contains("新游戏"):
		_fail("删除存档后选档界面没有刷新")
		return
	print("MAP_FLOW_SMOKE: PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("MAP_FLOW_SMOKE: " + message)
	quit(1)
