using System;
using Godot;

namespace Zaomeng.Events;

[GlobalClass]
public partial class VoidEventChannel : BaseEventChannel
{
	private readonly NotificationChannel<bool> _channel = new(ReportListenerError);
	public override int ListenerCount => _channel.ListenerCount;
	public IDisposable Subscribe(Action listener)
	{
		ArgumentNullException.ThrowIfNull(listener);
		return _channel.Subscribe(_ => listener());
	}
	public void Raise(object? sender = null)
	{
		RecordSender(sender);
		_channel.Raise(true);
	}
}
