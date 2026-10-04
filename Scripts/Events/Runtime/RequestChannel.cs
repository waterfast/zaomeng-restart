using System;

namespace Zaomeng.Events;

/// <summary>一对一的同步请求；没有处理者和嵌套请求均返回明确结果。</summary>
public sealed class RequestChannel<TRequest, TResult>(Func<TResult> unavailable, Func<TResult> busy)
{
	private Func<TRequest, TResult>? _handler;
	private bool _executing;
	public bool HasHandler => _handler is not null;

	public IDisposable RegisterHandler(Func<TRequest, TResult> handler)
	{
		ArgumentNullException.ThrowIfNull(handler);
		if (_handler is not null)
			throw new InvalidOperationException("请求通道已有处理者，请先释放旧连接。");
		_handler = handler;
		return new EventSubscription(() => _handler = null);
	}

	public TResult Send(TRequest request)
	{
		if (_executing) return busy();
		if (_handler is null) return unavailable();
		_executing = true;
		try { return _handler(request); }
		finally { _executing = false; }
	}
}
