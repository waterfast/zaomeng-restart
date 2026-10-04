using System;
using Godot;
using Zaomeng.Equipment;

namespace Zaomeng.Events;

[GlobalClass]
public partial class GemRequestChannel : BaseEventChannel
{
	private readonly RequestChannel<GemRequest, GemResult> _channel = new(GemResult.Unavailable, GemResult.Busy);
	public override int ListenerCount => _channel.HasHandler ? 1 : 0;
	public IDisposable RegisterHandler(Func<GemRequest, GemResult> handler) => _channel.RegisterHandler(handler);
	public GemResult Send(GemRequest request, object? sender = null)
	{
		ArgumentNullException.ThrowIfNull(request);
		RecordSender(sender);
		return _channel.Send(request);
	}
}
