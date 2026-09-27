using System;
using System.IO;

namespace Zaomeng.Save.Storage;

/// <summary>统一处理槽位路径、临时文件和上一份存档；子类只负责内容编码。</summary>
public abstract class FileSaveStorage : ISaveStorage
{
	private readonly string _directory;
	private readonly string _extension;

	protected FileSaveStorage(string directory, string extension)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		_directory = directory;
		_extension = extension;
	}

	public void Write(int slot, byte[] data, bool preserveBackup = false)
	{
		ArgumentNullException.ThrowIfNull(data);
		string path = GetPath(slot);
		string temporaryPath = path + ".tmp";
		string backupPath = path + ".bak";
		Directory.CreateDirectory(_directory);
		byte[] encoded = Encode(data);
		try
		{
			using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				stream.Write(encoded);
				stream.Flush(flushToDisk: true);
			}
			// 在替换正式文件前验证临时文件确实可以解码。
			if (!Decode(File.ReadAllBytes(temporaryPath)).AsSpan().SequenceEqual(data))
				throw new InvalidDataException("临时存档写入后校验失败。");
			if (File.Exists(path))
			{
				// 主文件已损坏时保留已有备份，不让坏文件覆盖它。
				if (!preserveBackup && CanDecode(path)) File.Replace(temporaryPath, path, backupPath);
				else File.Move(temporaryPath, path, overwrite: true);
			}
			else File.Move(temporaryPath, path);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	public byte[] Read(int slot) => Decode(File.ReadAllBytes(GetPath(slot)));
	public byte[] ReadBackup(int slot) => Decode(File.ReadAllBytes(GetPath(slot) + ".bak"));

	protected virtual byte[] Encode(byte[] data) => data;
	protected virtual byte[] Decode(byte[] data) => data;

	private bool CanDecode(string path)
	{
		try
		{
			Decode(File.ReadAllBytes(path));
			return true;
		}
		catch (InvalidDataException) { return false; }
		catch (System.Security.Cryptography.CryptographicException) { return false; }
	}

	private string GetPath(int slot)
	{
		if (slot < 1 || slot > 99)
			throw new ArgumentOutOfRangeException(nameof(slot), "存档槽位必须在 1 到 99 之间。");
		return Path.Combine(_directory, $"save_{slot:D2}.{_extension}");
	}
}
