using System;
using Godot;

namespace Zaomeng.Events;

[GlobalClass]
public partial class VoidEventListener : BaseEventListener
{
	[Export] public VoidEventChannel? EventChannel { get; set; }
	[Signal] public delegate void ReceivedEventHandler();
	protected override IDisposable? ConnectChannel() => EventChannel?.Subscribe(
		() => EmitSignal(SignalName.Received));
}
