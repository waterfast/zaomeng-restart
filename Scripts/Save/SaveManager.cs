using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Zaomeng.Save.Serialization;
using Zaomeng.Save.Storage;

namespace Zaomeng.Save;

/// <summary>只统筹存档数据和文件；运行时对象由各自的映射器转换。</summary>
public sealed class SaveManager(ISaveSerializer serializer, ISaveStorage storage)
{
	private readonly ISaveSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
	private readonly ISaveStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

	public void Save(int slot, GameSaveData data)
	{
		byte[] bytes = _serializer.Serialize(data);
		bool preserveBackup;
		try
		{
			_serializer.Deserialize(_storage.Read(slot));
			preserveBackup = false;
		}
		catch (Exception error) when (IsRecoverable(error))
		{
			// 原文件已经坏了时，不能让它在替换过程中覆盖最后一份可读备份。
			preserveBackup = true;
		}
		_storage.Write(slot, bytes, preserveBackup);
	}

	public GameSaveData Load(int slot)
	{
		try
		{
			return _serializer.Deserialize(_storage.Read(slot));
		}
		catch (Exception primaryError) when (IsRecoverable(primaryError))
		{
			try
			{
				return _serializer.Deserialize(_storage.ReadBackup(slot));
			}
			catch (Exception backupError) when (IsRecoverable(backupError))
			{
				throw new InvalidDataException("主存档和备份都无法读取。", new AggregateException(primaryError, backupError));
			}
		}
	}

	private static bool IsRecoverable(Exception error) =>
		error is IOException or CryptographicException or JsonException;
}
