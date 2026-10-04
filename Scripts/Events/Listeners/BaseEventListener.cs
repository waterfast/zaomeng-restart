using System;
using Godot;

namespace Zaomeng.Events;

/// <summary>对应 Unity 的启用/禁用订阅：进入场景树连接，离开时释放。</summary>
public abstract partial class BaseEventListener : Node
{
	private IDisposable? _connection;
	protected abstract IDisposable? ConnectChannel();
	public override void _EnterTree() => _connection = ConnectChannel();
	public override void _ExitTree()
	{
		_connection?.Dispose();
		_connection = null;
	}
}
