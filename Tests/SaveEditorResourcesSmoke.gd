extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not Engine.is_editor_hint():
		_fail("必须使用 --editor 运行，普通游戏模式不能验证工具资源")
		return
	var panel_script := load("res://addons/SaveEditor/SaveEditorPanel.cs")
	var panel: Control = panel_script.new()
	root.add_child(panel)
	# 等待扫描、导入及面板延后加载；不能只依靠进程退出码判断编辑器错误。
	var selector: OptionButton
	for frame in range(600):
		await process_frame
		for child in panel.find_children("*", "", true, false):
			if child is Label and child.text.begins_with("物品目录加载失败"):
				_fail(child.text)
				return
			if child is OptionButton and child.item_count > 6:
				selector = child
		if selector != null:
			break
	if selector == null:
		_fail("编辑器中的物品目录未就绪")
		return
	var catalog := load("res://Content/Items/ItemCatalog.tres")
	if catalog == null:
		_fail("目录加载失败")
		return
	# 所有递归配置脚本都必须可在编辑器实例化，新增资源也纳入检查。
	if not _check_resource(catalog, {}):
		return
	var earth_fury := load("res://Content/GameData/EquipmentSkills/earth_fury.tres")
	if earth_fury.Effect.Modifiers.DamageMultiplier != 1.5:
		_fail("编辑器未读取被动效果的策划数值")
		return
	var ability := load("res://Content/MagicWeapons/dshl.tres")
	if ability.Action.Id != "magic_dshl" or ability.Action.Growth.InitialManaCost != 50:
		_fail("编辑器未读取法宝动作或成长资源")
		return
	var initial_count := selector.item_count
	EditorInterface.get_resource_filesystem().reimport_files(PackedStringArray([
		"res://Assets/Art/BackPack/AllItems/dshl.png"
	]))
	await process_frame
	await process_frame
	for iteration in range(2):
		for child in panel.find_children("*", "", true, false):
			if child is Button and child.text == "刷新":
				child.pressed.emit()
		await process_frame
		await process_frame
		if selector.item_count != initial_count:
			_fail("刷新目录后物品丢失或重复")
			return
	panel.free()
	print("SAVE_EDITOR_RESOURCES_SMOKE: PASS (editor mode)")
	quit(0)


func _check_resource(resource: Resource, visited: Dictionary) -> bool:
	if resource == null or visited.has(resource.get_instance_id()):
		return true
	visited[resource.get_instance_id()] = true
	var script = resource.get_script()
	if script != null and not script.is_tool():
		_fail("编辑器引用的配置脚本缺少 Tool：" + script.resource_path)
		return false
	for property in resource.get_property_list():
		if property.name == "script" or not (property.usage & PROPERTY_USAGE_STORAGE):
			continue
		var value = resource.get(property.name)
		if value is Resource and not _check_resource(value, visited):
			return false
		if value is Array:
			for entry in value:
				if entry is Resource and not _check_resource(entry, visited):
					return false
	return true


func _fail(message: String) -> void:
	push_error("SAVE_EDITOR_RESOURCES_SMOKE: " + message)
	quit(1)
