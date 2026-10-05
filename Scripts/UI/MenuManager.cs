using System;
using System.Collections.Generic;
using Godot;

namespace Zaomeng.UI;

/// <summary>统一管理测试场景的菜单快捷键、互斥显示和游戏暂停。</summary>
public partial class MenuManager : Node
{
	private readonly Dictionary<StringName, CanvasItem> _menus = new();
	private CanvasItem? _activeMenu;
	private bool _wasPausedBeforeMenu;

	public bool IsMenuOpen => _activeMenu is not null;
	public event Action<bool>? MenuStateChanged;

	public override void _Ready()
	{
		// 场景树暂停后仍要接收快捷键，才能用同一个按键关掉菜单。
		ProcessMode = ProcessModeEnum.Always;
	}

	public void RegisterMenu(StringName inputAction, CanvasItem menu)
	{
		ArgumentNullException.ThrowIfNull(menu);
		if (!InputMap.HasAction(inputAction))
			throw new ArgumentException($"未定义菜单输入动作：{inputAction}", nameof(inputAction));
		if (!_menus.TryAdd(inputAction, menu))
			throw new InvalidOperationException($"菜单输入动作已注册：{inputAction}");

		// 只让菜单分支继续处理输入和按钮；角色与怪物仍按默认模式暂停。
		menu.ProcessMode = ProcessModeEnum.Always;
		menu.Hide();
	}

	public override void _Input(InputEvent input)
	{
		if (input is InputEventKey { Echo: true })
			return;
		if (_activeMenu is not null && input is InputEventKey { Pressed: true, Keycode: Key.Escape })
		{
			CloseMenu();
			GetViewport().SetInputAsHandled();
			return;
		}
		foreach ((StringName action, CanvasItem menu) in _menus)
		{
			if (!input.IsActionPressed(action))
				continue;
			if (_activeMenu == menu)
				CloseMenu();
			else
				OpenMenu(menu);
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	public void CloseMenu()
	{
		if (_activeMenu is null)
			return;
		_activeMenu.Hide();
		_activeMenu = null;
		GetTree().Paused = _wasPausedBeforeMenu;
		MenuStateChanged?.Invoke(false);
	}

	public void ToggleMenu(StringName action)
	{
		if (!_menus.TryGetValue(action, out CanvasItem? menu)) return;
		if (_activeMenu == menu) CloseMenu();
		else OpenMenu(menu);
	}

	public override void _ExitTree()
	{
		// 场景在菜单打开时退出，也不能把暂停状态遗留给下一个场景。
		if (_activeMenu is not null)
			GetTree().Paused = _wasPausedBeforeMenu;
	}

	private void OpenMenu(CanvasItem menu)
	{
		if (_activeMenu is null)
			_wasPausedBeforeMenu = GetTree().Paused;
		else
			_activeMenu.Hide();

		_activeMenu = menu;
		menu.Show();
		GetTree().Paused = true;
		MenuStateChanged?.Invoke(true);
	}
}
