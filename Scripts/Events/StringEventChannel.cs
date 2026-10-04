using System;
using Godot;

namespace Zaomeng.Events;

[GlobalClass]
public partial class StringEventChannel : BaseEventChannel
{
	private readonly NotificationChannel<string> _channel = new(ReportListenerError);
	public override int ListenerCount => _channel.ListenerCount;
	public IDisposable Subscribe(Action<string> listener) => _channel.Subscribe(listener);
	public void Raise(string value, object? sender = null)
	{
		RecordSender(sender);
		_channel.Raise(value);
	}
}
