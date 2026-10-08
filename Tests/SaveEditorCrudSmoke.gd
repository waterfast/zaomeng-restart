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
	var clear_button: Button
	var refresh_button: Button
	var apply_button: Button
	var inventory: ItemList
	var json: TextEdit
	var souls: LineEdit
	var coupons: LineEdit
	var status: Label
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
		elif child is Button and child.text == "清空选中槽位":
			clear_button = child
		elif child is Button and child.text == "刷新":
			refresh_button = child
		elif child is Button and child.text == "应用 JSON":
			apply_button = child
		elif child is ItemList:
			inventory = child
		elif child is TextEdit:
			json = child
		elif child is Label and child.text.begins_with("选择槽位"):
			status = child
		elif child is LineEdit and child.get_parent().get_child_count() > 0 and child.get_parent().get_child(0) is Label:
			if child.get_parent().get_child(0).text == "灵魂":
				souls = child
			elif child.get_parent().get_child(0).text == "点券":
				coupons = child
	if slot == null or level == null or existing_saves == null or add_button == null or read_button == null or save_button == null or delete_button == null or confirmation == null or clear_button == null or refresh_button == null or apply_button == null or inventory == null or json == null or souls == null or coupons == null or status == null:
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
	# 清空槽位刷新 JSON 前，必须保留刚修改的表单。
	souls.text = "12345"
	level.value = 101
	inventory.select(0)
	clear_button.pressed.emit()
	var edited = JSON.parse_string(json.text)
	if edited.wallet.souls != 12345 or edited.characters[0].level != 101:
		_fail("清空槽位丢失了角色或货币的表单修改")
		return
	# 表单校验失败不能提交一部分；未应用的 JSON 不得被刷新或编辑覆盖。
	souls.text = "999"
	coupons.text = "invalid"
	inventory.select(0)
	clear_button.pressed.emit()
	if not status.text.contains("点券必须是非负整数"):
		_fail("非法表单没有被拒绝：" + status.text)
		return
	refresh_button.pressed.emit()
	if JSON.parse_string(json.text).wallet.souls != 12345:
		_fail("表单校验失败后发生部分提交")
		return
	coupons.text = "0"
	json.text = json.text.replace('"souls": 12345', '"souls": 54321')
	# 赋值 Text 不会触发用户输入信号，显式模拟实际编辑。
	json.text_changed.emit()
	var pending_json := json.text
	refresh_button.pressed.emit()
	clear_button.pressed.emit()
	if json.text != pending_json:
		_fail("未应用的 JSON 被其他操作覆盖")
		return
	apply_button.pressed.emit()
	if not status.text.begins_with("JSON 已应用"):
		_fail("JSON 应用失败：" + status.text)
		return
	save_button.pressed.emit()
	read_button.pressed.emit()
	if souls.text != "54321" or level.value != 101:
		_fail("连续编辑保存后读档数据不一致")
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
	panel.free()
	print("SAVE_EDITOR_CRUD_SMOKE: PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("SAVE_EDITOR_CRUD_SMOKE: " + message)
	quit(1)
