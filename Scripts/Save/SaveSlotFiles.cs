using System;
using System.Collections.Generic;
using System.IO;

namespace Zaomeng.Save;

public sealed record SaveSlotFile(int Slot, string Extension, bool BackupOnly);

/// <summary>只操作默认存档命名规则下的文件，删除槽位时一并清理备份和临时文件。</summary>
public static class SaveSlotFiles
{
	private static readonly string[] Extensions = ["json", "dat"];

	public static IReadOnlyList<SaveSlotFile> List(string directory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		var files = new List<SaveSlotFile>();
		for (int slot = 1; slot <= 99; slot++)
		{
			foreach (string extension in Extensions)
			{
				string path = GetPath(directory, slot, extension);
				bool mainExists = File.Exists(path);
				if (mainExists || File.Exists(path + ".bak"))
					files.Add(new SaveSlotFile(slot, extension, !mainExists));
			}
		}
		return files;
	}

	public static bool Exists(string directory, int slot, string extension)
	{
		string path = GetPath(directory, slot, extension);
		return File.Exists(path) || File.Exists(path + ".bak");
	}

	public static bool Delete(string directory, int slot)
	{
		bool deleted = false;
		foreach (string extension in Extensions)
		{
			string path = GetPath(directory, slot, extension);
			foreach (string suffix in new[] { "", ".bak", ".tmp" })
			{
				string file = path + suffix;
				if (!File.Exists(file)) continue;
				File.Delete(file);
				deleted = true;
			}
		}
		return deleted;
	}

	public static string GetPath(string directory, int slot, string extension)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		if (slot is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(slot));
		if (extension is not ("json" or "dat")) throw new ArgumentException("不支持的存档格式。", nameof(extension));
		return Path.Combine(directory, $"save_{slot:D2}.{extension}");
	}
}
