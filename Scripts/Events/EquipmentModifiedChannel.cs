using System;
using Godot;
using Zaomeng.Equipment;

namespace Zaomeng.Events;

[GlobalClass]
public partial class EquipmentModifiedChannel : BaseEventChannel
{
	private readonly NotificationChannel<EquipmentModification> _channel = new(ReportListenerError);
	public override int ListenerCount => _channel.ListenerCount;
	public IDisposable Subscribe(Action<EquipmentModification> listener) => _channel.Subscribe(listener);
	public void Raise(EquipmentModification value, object? sender = null)
	{
		RecordSender(sender);
		_channel.Raise(value);
	}
}
