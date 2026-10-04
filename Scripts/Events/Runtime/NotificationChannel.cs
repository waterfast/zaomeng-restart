using System;
using System.Collections.Generic;

namespace Zaomeng.Events;

/// <summary>一对多的事实通知；每个监听者独立执行，不参与业务提交。</summary>
public sealed class NotificationChannel<T>(Action<Exception> reportError)
{
	private readonly List<Action<T>> _listeners = new();
	public int ListenerCount => _listeners.Count;

	public IDisposable Subscribe(Action<T> listener)
	{
		ArgumentNullException.ThrowIfNull(listener);
		// 包装一次，使同一个方法的两次订阅也拥有独立的释放句柄。
		Action<T> connection = value => listener(value);
		_listeners.Add(connection);
		return new EventSubscription(() => _listeners.Remove(connection));
	}

	public void Raise(T value)
	{
		foreach (Action<T> listener in _listeners.ToArray())
		{
			if (!_listeners.Contains(listener)) continue;
			try { listener(value); }
			catch (Exception error) { reportError(error); }
		}
	}
}
