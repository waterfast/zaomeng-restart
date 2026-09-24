extends SceneTree

# One-off migration tool. Gameplay is entirely C#.
func _initialize():
	var entries = JSON.parse_string(FileAccess.get_file_as_string("res://Tools/texture_manifest.json"))
	var failures = []
	var count = 0
	for entry in entries:
		var destination: String = ProjectSettings.globalize_path(entry.destination)
		if FileAccess.file_exists(destination):
			continue
		var texture = CompressedTexture2D.new()
		if texture.load(entry.source) != OK:
			failures.append(entry.source)
			continue
		var pixels = texture.get_image()
		if pixels == null or pixels.is_empty():
			failures.append(entry.source)
			continue
		if pixels.is_compressed():
			pixels.decompress()
		DirAccess.make_dir_recursive_absolute(destination.get_base_dir())
		if pixels.save_png(destination) != OK:
			failures.append(destination)
		count += 1
		if count % 100 == 0:
			print("Recovered ", count, " textures")
	var report = {"recovered": count, "failures": failures}
	FileAccess.open("res://Tools/recovery_report.json", FileAccess.WRITE).store_string(JSON.stringify(report, "  "))
	print(report)
	quit(0 if failures.is_empty() else 1)
