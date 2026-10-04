using System;
using Godot;
using Zaomeng.Equipment;

namespace Zaomeng.Events;

[GlobalClass]
public partial class EquipmentChangedChannel : BaseEventChannel
{
	private readonly NotificationChannel<EquipmentChange> _channel = new(ReportListenerError);
	public override int ListenerCount => _channel.ListenerCount;
	public IDisposable Subscribe(Action<EquipmentChange> listener) => _channel.Subscribe(listener);
	public void Raise(EquipmentChange value, object? sender = null)
	{
		RecordSender(sender);
		_channel.Raise(value);
	}
}
