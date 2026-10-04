using Godot;

namespace Zaomeng.Events;

/// <summary>对应 Unity 的事件 SO；资源保存说明，连接和调试状态仅存在于运行时。</summary>
public abstract partial class BaseEventChannel : Resource
{
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	public string LastSender { get; private set; } = "";
	public int RaiseCount { get; private set; }
	public abstract int ListenerCount { get; }

	// 只在检查器显示，避免订阅数和发送者被写进 .tres 资源。
	public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList() =>
	[
		DebugProperty("Runtime/ListenerCount", Variant.Type.Int),
		DebugProperty("Runtime/LastSender", Variant.Type.String),
		DebugProperty("Runtime/RaiseCount", Variant.Type.Int)
	];

	private static Godot.Collections.Dictionary DebugProperty(string name, Variant.Type type) => new()
	{
		["name"] = name, ["type"] = (int)type,
		["usage"] = (int)(PropertyUsageFlags.Editor | PropertyUsageFlags.ReadOnly)
	};

	public override Variant _Get(StringName property) => property.ToString() switch
	{
		"Runtime/ListenerCount" => ListenerCount,
		"Runtime/LastSender" => LastSender,
		"Runtime/RaiseCount" => RaiseCount,
		_ => default
	};

	protected void RecordSender(object? sender)
	{
		LastSender = sender?.ToString() ?? "未知发送者";
		RaiseCount++;
	}

	protected static void ReportListenerError(System.Exception error)
		=> GD.PushError($"事件监听者执行失败：{error}");
}
