using System;
using Godot;
using Zaomeng.Equipment;

namespace Zaomeng.Events;

[GlobalClass]
public partial class EquipmentRequestChannel : BaseEventChannel
{
	private readonly RequestChannel<EquipmentRequest, EquipmentResult> _channel =
		new(EquipmentResult.Unavailable, EquipmentResult.Busy);
	public override int ListenerCount => _channel.HasHandler ? 1 : 0;
	public IDisposable RegisterHandler(Func<EquipmentRequest, EquipmentResult> handler)
		=> _channel.RegisterHandler(handler);
	public EquipmentResult Send(EquipmentRequest request, object? sender = null)
	{
		ArgumentNullException.ThrowIfNull(request);
		RecordSender(sender);
		return _channel.Send(request);
	}
}
