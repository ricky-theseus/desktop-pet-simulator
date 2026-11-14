using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopPet.Runtime;

/// <summary>只调整本进程桌宠主窗口的任务栏样式；其他平台不调用 Win32。</summary>
internal static class WindowsDesktopWindow
{
    private const int ExtendedStyle = -20;
    private const int AppWindow = 0x00040000;
    private const int ToolWindow = 0x00000080;

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
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint handle, int command);
}
