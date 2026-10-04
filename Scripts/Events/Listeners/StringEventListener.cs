using System;
using Godot;

namespace Zaomeng.Events;

[GlobalClass]
public partial class StringEventListener : BaseEventListener
{
	[Export] public StringEventChannel? EventChannel { get; set; }
	[Signal] public delegate void ReceivedEventHandler(string value);
	protected override IDisposable? ConnectChannel() => EventChannel?.Subscribe(
		value => EmitSignal(SignalName.Received, value));
}
