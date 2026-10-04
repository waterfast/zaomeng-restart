using System;

namespace Zaomeng.Events;

/// <summary>释放时只移除本次连接；重复释放不会影响其他订阅。</summary>
public sealed class EventSubscription(Action disconnect) : IDisposable
{
	private Action? _disconnect = disconnect;
	public void Dispose()
	{
		Action? disconnect = _disconnect;
		_disconnect = null;
		disconnect?.Invoke();
	}
}
