#if TOOLS
using Godot;

namespace Zaomeng.EditorTools;

[Tool]
public partial class SaveEditorPlugin : EditorPlugin
{
	private EditorDock? _dock;
	private SaveEditorPanel? _panel;

	public override void _EnterTree()
	{
		_panel = new SaveEditorPanel();
		_dock = new EditorDock { Title = "存档管理", DefaultSlot = EditorDock.DockSlot.Bottom };
		_dock.AddChild(_panel);
		AddDock(_dock);
	}

	public override void _ExitTree()
	{
		if (_dock is null) return;
		RemoveDock(_dock);
		_dock.QueueFree();
		_dock = null;
		_panel = null;
	}
}
#endif
