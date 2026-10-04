using System;
using System.Collections.Generic;
using Godot;
using Zaomeng.Items;

namespace Zaomeng.Events;

[GlobalClass]
public partial class ItemActionRequestChannel : BaseEventChannel
{
	private readonly RequestChannel<ItemActionRequest, ItemActionResult> _channel =
		new(ItemActionResult.Unavailable, ItemActionResult.Busy);
	private Func<ItemActionTarget, IReadOnlyList<ItemActionOption>>? _options;
	public override int ListenerCount => _channel.HasHandler ? 1 : 0;
	public IDisposable RegisterHandler(Func<ItemActionRequest, ItemActionResult> handler,
		Func<ItemActionTarget, IReadOnlyList<ItemActionOption>> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		IDisposable connection = _channel.RegisterHandler(handler);
		_options = options;
		return new EventSubscription(() => { connection.Dispose(); _options = null; });
	}
	public IReadOnlyList<ItemActionOption> GetOptions(ItemActionTarget target)
		=> _options?.Invoke(target) ?? Array.Empty<ItemActionOption>();
	public ItemActionResult Send(ItemActionRequest request, object? sender = null)
	{
		ArgumentNullException.ThrowIfNull(request);
		RecordSender(sender);
		return _channel.Send(request);
	}
}
