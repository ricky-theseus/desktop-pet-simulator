using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopPet.Runtime;

/// <summary>处理本进程桌宠窗口的任务栏样式及所属工具窗口；其他平台不调用 Win32。</summary>
internal static class WindowsDesktopWindow
{
    private const int ExtendedStyle = -20;
    private const int AppWindow = 0x00040000;
    private const int ToolWindow = 0x00000080;
    private const int WindowOwner = -8;

    public static void ShowOwnedWindow(nint handle, nint owner)
    {
        if (!OperatingSystem.IsWindows()) return;
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(handle, WindowOwner, owner);
        var error = Marshal.GetLastWin32Error();
        if (previous == 0 && error != 0) throw new Win32Exception(error);
        // 隐藏启动参数可能覆盖 Godot 首次 ShowWindow；显式再次显示同步原生可见性。
        // 设置 owner 可保留正常标题栏，同时避免管理器产生独立任务栏按钮。
        ShowWindow(handle, IsIconic(handle) ? 9 : 5);
    }

    public static void HideTaskbarButton(nint handle)
    {
        if (!OperatingSystem.IsWindows()) return;
        var style = GetWindowLong(handle, ExtendedStyle);
        // Windows 要求隐藏后更改样式再显示，才能刷新已有的任务栏按钮。
        ShowWindow(handle, 0);
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLong(handle, ExtendedStyle, (style & ~AppWindow) | ToolWindow);
        var error = Marshal.GetLastWin32Error();
        ShowWindow(handle, 4);
        if (previous == 0 && error != 0) throw new Win32Exception(error);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(nint handle, int index, int value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint handle, int index, nint value);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint handle, int command);
}
