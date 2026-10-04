using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;

namespace Zaomeng.Equipment;

/// <summary>玩家拥有的一件装备。不可变快照避免背包、事件和存档共享可修改的宝石列表。</summary>
public sealed record EquipmentInstance(string InstanceId, string DefinitionId, ImmutableArray<string> SocketedGemIds)
{
	public static EquipmentInstance Create(string definitionId, int socketCount)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
		ArgumentOutOfRangeException.ThrowIfNegative(socketCount);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(socketCount, 8);
		return new(Guid.NewGuid().ToString("N"), definitionId,
			Enumerable.Repeat("", socketCount).ToImmutableArray());
	}

	internal EquipmentInstance WithGem(int socket, string gemId)
		=> this with { SocketedGemIds = SocketedGemIds.SetItem(socket, gemId) };

	[JsonIgnore]
	public bool IsValid => !string.IsNullOrWhiteSpace(InstanceId) && !string.IsNullOrWhiteSpace(DefinitionId) &&
		!SocketedGemIds.IsDefault && SocketedGemIds.All(id => id is not null && (id.Length == 0 || !string.IsNullOrWhiteSpace(id)));
}
