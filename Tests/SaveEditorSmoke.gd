extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var panel_script := load("res://addons/SaveEditor/SaveEditorPanel.cs")
	if panel_script == null:
		_fail("无法加载存档管理面板脚本")
		return
	var panel: Control = panel_script.new()
	root.add_child(panel)
	await process_frame

	var status: Label
	var slot: SpinBox
	var read_button: Button
	var new_button: Button
	var existing_saves: OptionButton
	var item_selector: OptionButton
	for child in panel.find_children("*", "", true, false):
		if child is Label and child.text.begins_with("选择槽位"):
			status = child
		elif child is Label and (child.text.begins_with("物品目录") or child.text.begins_with("无法加载物品目录")):
			_fail(child.text)
			return
		elif child is SpinBox and slot == null:
			slot = child
		elif child is Button and child.text == "添加存档":
			new_button = child
		elif child is Button and child.text == "读取":
			read_button = child
		elif child is OptionButton and child.item_count > 0 and child.get_item_text(0) == "选择默认目录中的存档":
			existing_saves = child
		elif child is OptionButton and child.item_count >= 6:
			item_selector = child

	if status == null or slot == null or read_button == null or new_button == null or existing_saves == null or item_selector == null:
		_fail("存档面板或物品目录没有正确初始化")
		return
	if FileAccess.file_exists("user://saves/save_01.json"):
		if existing_saves.item_count < 2:
			_fail("默认目录中的一号存档没有显示在列表中")
			return
		read_button.pressed.emit()
		if not status.text.begins_with("已读取存档"):
			_fail("现有一号存档读取失败：" + status.text)
			return
	slot.value = 99
	new_button.pressed.emit()
	if not status.text.begins_with("新存档已创建"):
		_fail("无法在内存中新建存档：" + status.text)
		return

	print("SAVE_EDITOR_SMOKE: PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error("SAVE_EDITOR_SMOKE: " + message)
	quit(1)
