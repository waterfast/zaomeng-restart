using System;
using Godot;

namespace Zaomeng.Events;

[GlobalClass]
public partial class IntEventChannel : BaseEventChannel
{
	private readonly NotificationChannel<int> _channel = new(ReportListenerError);
	public override int ListenerCount => _channel.ListenerCount;
	public IDisposable Subscribe(Action<int> listener) => _channel.Subscribe(listener);
	public void Raise(int value, object? sender = null)
	{
		RecordSender(sender);
		_channel.Raise(value);
	}
}
