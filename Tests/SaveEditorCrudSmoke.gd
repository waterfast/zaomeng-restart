extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	# 插件增删会写入 user://，只允许在测试专用用户目录执行。
	if not ProjectSettings.globalize_path("user://").replace("\\", "/").contains("/.godot/test-appdata/"):
		_fail("需要独立的用户数据目录")
		return
	var panel_script := load("res://addons/SaveEditor/SaveEditorPanel.cs")
	var panel: Control = panel_script.new()
	root.add_child(panel)
	await process_frame

	var slot: SpinBox
	var level: SpinBox
	var existing_saves: OptionButton
	var add_button: Button
	var read_button: Button
	var save_button: Button
	var delete_button: Button
	var confirmation: ConfirmationDialog
	for child in panel.find_children("*", "", true, false):
		if child is SpinBox and slot == null:
			slot = child
		elif child is SpinBox and child.get_parent().get_child(0) is Label and child.get_parent().get_child(0).text == "等级":
			level = child
		elif child is OptionButton and child.item_count > 0 and child.get_item_text(0) == "选择默认目录中的存档":
			existing_saves = child
		elif child is Button and child.text == "添加存档":
			add_button = child
		elif child is Button and child.text == "读取":
			read_button = child
		elif child is Button and child.text == "保存":
			save_button = child
		elif child is Button and child.text == "删除存档":
			delete_button = child
		elif child is ConfirmationDialog:
			confirmation = child
	if slot == null or level == null or existing_saves == null or add_button == null or read_button == null or save_button == null or delete_button == null or confirmation == null:
		_fail("插件增删控件没有正确初始化")
		return

	slot.value = 97
	var save_path := "user://saves/save_97.json"
	if FileAccess.file_exists(save_path) or FileAccess.file_exists(save_path + ".bak"):
		_fail("测试槽位已有存档")
		return
	add_button.pressed.emit()
	level.value = 100
	save_button.pressed.emit()
	if not FileAccess.file_exists(save_path):
		_fail("插件没有写入新存档")
		return
	var listed := false
	for index in range(existing_saves.item_count):
		if existing_saves.get_item_text(index).contains("97 · JSON · 孙悟空 Lv.100"):
			listed = true
	if not listed:
		_fail("存档列表没有刷新角色等级")
		return
	read_button.pressed.emit()
	if level.value != 100:
		_fail("插件没有读取修改后的等级")
		return
	delete_button.pressed.emit()
	confirmation.confirmed.emit()
	if FileAccess.file_exists(save_path) or FileAccess.file_exists(save_path + ".bak"):
		_fail("插件没有删除存档及备份")
		return
	for index in range(existing_saves.item_count):
		if existing_saves.get_item_text(index).begins_with("97 ·"):
			_fail("删除后存档仍留在列表")
			return
	print("SAVE_EDITOR_CRUD_SMOKE: PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("SAVE_EDITOR_CRUD_SMOKE: " + message)
	quit(1)
