extends SceneTree

# 数字原图缺失；按旧场景引用的导入映射恢复，不按文件名猜测替代图片。
func _initialize():
	var old_root = ProjectSettings.globalize_path("res://../造梦八荒-(4.1)")
	var destination = "res://Assets/Art/AllNumber/LJ"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(destination))
	var pattern = RegEx.new()
	pattern.compile('path="res://([^"]+\\.ctex)"')
	for name in ["lj_0", "lj_1", "lj_2", "lj_3", "lj_4", "lj_5", "lj_6", "lj_7", "lj_8", "lj_9", "lj_text"]:
		var match = pattern.search(FileAccess.get_file_as_string(old_root + "/Art/AllNumber/LJ/" + name + ".png.import"))
		if match == null:
			quit(1)
			return
		var texture = CompressedTexture2D.new()
		if texture.load(old_root + "/" + match.get_string(1)) != OK:
			quit(1)
			return
		var pixels = texture.get_image()
		if pixels.is_compressed(): pixels.decompress()
		if pixels.save_png(destination + "/" + name + ".png") != OK:
			quit(1)
			return
	print("Recovered original combo digits and label.")
	quit()
