extends SceneTree

func _initialize():
	var entries = JSON.parse_string(FileAccess.get_file_as_string("res://Tools/shop_alchemy_textures.json"))
	for entry in entries:
		var texture = CompressedTexture2D.new()
		if texture.load(entry.source) != OK:
			push_error(entry.source)
			quit(1)
			return
		var pixels = texture.get_image()
		if pixels.is_compressed(): pixels.decompress()
		var destination = ProjectSettings.globalize_path(entry.destination)
		DirAccess.make_dir_recursive_absolute(destination.get_base_dir())
		if pixels.save_png(destination) != OK:
			quit(1)
			return
	print("Recovered shop/alchemy textures: ", entries.size())
	quit()
