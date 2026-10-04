using System;
using Godot;

namespace Zaomeng.Events;

[GlobalClass]
public partial class IntEventListener : BaseEventListener
{
	[Export] public IntEventChannel? EventChannel { get; set; }
	[Signal] public delegate void ReceivedEventHandler(int value);
	protected override IDisposable? ConnectChannel() => EventChannel?.Subscribe(
		value => EmitSignal(SignalName.Received, value));
}
