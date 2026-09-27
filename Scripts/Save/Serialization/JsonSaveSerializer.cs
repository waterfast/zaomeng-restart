using System;
using System.IO;
using System.Text.Json;

namespace Zaomeng.Save.Serialization;

public sealed class JsonSaveSerializer : ISaveSerializer
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public byte[] Serialize(GameSaveData data)
	{
		SaveDataValidator.Validate(data);
		return JsonSerializer.SerializeToUtf8Bytes(data, Options);
	}

	public GameSaveData Deserialize(byte[] bytes)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		GameSaveData data = JsonSerializer.Deserialize<GameSaveData>(bytes, Options)
			?? throw new InvalidDataException("存档内容为空。");
		SaveDataValidator.Validate(data);
		return data;
	}
}
