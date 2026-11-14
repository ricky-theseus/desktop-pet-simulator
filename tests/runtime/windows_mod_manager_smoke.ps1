param([Parameter(Mandatory)][string]$GodotExecutable)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw '此检查需要 Windows 原生窗口。' }
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class NativeModWindowTest {
    public delegate bool Callback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static Point ClientSize(IntPtr window) {
        GetClientRect(window, out var rect);
        return new Point { X = rect.Right, Y = rect.Bottom };
    }
    public static void MoveToControl(IntPtr window, uint target, int x, int y, bool activate = true) {
        GetWindowThreadProcessId(window, out var actual);
        GetClientRect(window, out var rect);
        if (actual != target || !IsWindowVisible(window) || x < 0 || y < 0 || x >= rect.Right || y >= rect.Bottom)
            throw new InvalidOperationException("鼠标位置必须位于本次测试创建的可见窗口内。");
        if (activate) SetForegroundWindow(window);
        var point = new Point { X = x, Y = y };
        ClientToScreen(window, ref point);
        SetCursorPos(point.X, point.Y);
    }
    public static void ClickNativePopup(IntPtr window, uint target, int x, int y) {
        // 系统菜单不能抢焦点；点击前再次确认真实光标下是本次测试的菜单窗口。
        MoveToControl(window, target, x, y, false);
        GetCursorPos(out var point);
        if (WindowFromPoint(point) != window) throw new InvalidOperationException("测试菜单被其他窗口遮挡，取消点击。");
        mouse_event(0x2, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x4, 0, 0, 0, UIntPtr.Zero);
    }
    public static void DragTitleBar(IntPtr window, uint target) {
        MoveToControl(window, target, 100, 30);
        GetWindowRect(window, out var bounds);
        var client = new Point();
        ClientToScreen(window, ref client);
        var x = bounds.Left + 180;
        var y = (bounds.Top + client.Y) / 2;
        SetCursorPos(x, y);
        GetCursorPos(out var point);
        if (WindowFromPoint(point) != window) throw new InvalidOperationException("标题栏被遮挡，取消拖动测试。");
        mouse_event(0x2, 0, 0, 0, UIntPtr.Zero);
        try {
            System.Threading.Thread.Sleep(100);
            SetCursorPos(x + 60, y + 40);
            System.Threading.Thread.Sleep(150);
        } finally { mouse_event(0x4, 0, 0, 0, UIntPtr.Zero); }
    }
    [StructLayout(LayoutKind.Sequential)] struct IconIdentifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rect rect);
    public static int TrayId(IntPtr window) {
        // 独立测试进程仅有一个托盘图标，查询本进程窗口下的 ID，不访问其他应用。
        for (uint id = 0; id < 8; id++) {
            var identifier = new IconIdentifier { Size = (uint)Marshal.SizeOf<IconIdentifier>(), Window = window, Id = id };
            if (Shell_NotifyIconGetRect(ref identifier, out var rect) == 0 && rect.Right > rect.Left) return (int)id;
        }
        return -1;
    }
    public static Dictionary<long, string> Windows(uint target) {
        var result = new Dictionary<long, string>();
        EnumWindows((handle, parameter) => {
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == target) {
                var text = new StringBuilder(256);
                GetWindowText(handle, text, 256);
                result.Add(handle.ToInt64(), text.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@
[NativeModWindowTest]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$originalCursor = [NativeModWindowTest+Point]::new()
[NativeModWindowTest]::GetCursorPos([ref]$originalCursor) | Out-Null
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$profile = Join-Path ([System.IO.Path]::GetTempPath()) ('dps-native-ui-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $profile | Out-Null
$log = Join-Path $profile 'engine.log'
$launchArguments = '--path "{0}" --log-file "{1}" -- --mod-data-dir="{2}" --open-mod-manager' -f (Join-Path $repo 'runtime'), $log, $profile
# 隐藏启动可复现启动器提供 SW_HIDE 时，Godot 内部可见但原生窗口隐藏的情况。
$process = Start-Process -FilePath $GodotExecutable -ArgumentList $launchArguments -WindowStyle Hidden -PassThru
function Wait-Condition([scriptblock]$Condition, [string]$Failure) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        if (& $Condition) { return }
        if ($process.HasExited) { throw "桌宠提前退出：$log" }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    throw $Failure
}
function Find-Window([string]$Title) {
    foreach ($entry in [NativeModWindowTest]::Windows($process.Id).GetEnumerator()) {
        if ($entry.Value -eq $Title) { return [IntPtr]$entry.Key }
    }
    return [IntPtr]::Zero
}
function Click-Client([IntPtr]$Handle, [int]$X, [int]$Y, [bool]$Right = $false) {
    # Godot 按真实光标判定窗口进入和按钮悬停，只有消息坐标不能触发开关点击。
    [NativeModWindowTest]::MoveToControl($Handle, $process.Id, $X, $Y)
    $point = [IntPtr](($Y -shl 16) -bor ($X -band 65535))
    $down = if ($Right) { 0x204 } else { 0x201 }
    $up = if ($Right) { 0x205 } else { 0x202 }
    $button = if ($Right) { 2 } else { 1 }
    [NativeModWindowTest]::PostMessage($Handle, 0x200, [IntPtr]0, $point) | Out-Null
    Start-Sleep -Milliseconds 100
    [NativeModWindowTest]::PostMessage($Handle, $down, [IntPtr]$button, $point) | Out-Null
    Start-Sleep -Milliseconds 100
    [NativeModWindowTest]::PostMessage($Handle, $up, [IntPtr]0, $point) | Out-Null
}
function Open-ManagerFromMenu([IntPtr]$PetHandle) {
    Click-Client $PetHandle 96 145 $true
    Wait-Condition { (Find-Window '') -ne [IntPtr]::Zero } '右键菜单未创建。'
    $menu = Find-Window ''
    Wait-Condition { [NativeModWindowTest]::IsWindowVisible($menu) } '右键菜单未显示。'
    # 跨过菜单最短显示时间，避免同一帧按下/释放被当成打开菜单的那次输入。
    Start-Sleep -Milliseconds 400
    Click-Client $menu 80 114
    Wait-Condition {
        $handle = Find-Window 'Mod 管理器'
        $handle -ne [IntPtr]::Zero -and [NativeModWindowTest]::IsWindowVisible($handle) -and -not [NativeModWindowTest]::IsIconic($handle) -and [NativeModWindowTest]::GetWindow($handle, 4) -eq [IntPtr]::Zero
    } '选择菜单项后管理器没有显示。'
    return Find-Window 'Mod 管理器'
}
try {
    Wait-Condition { (Find-Window 'Mod 管理器') -ne [IntPtr]::Zero } '管理窗口未创建。'
    $manager = Find-Window 'Mod 管理器'
    $pet = Find-Window '桌面宠物模拟器 (DEBUG)'
    Wait-Condition { [NativeModWindowTest]::IsWindowVisible($manager) } '管理器内部已打开，但原生窗口仍然隐藏。'
    if (([NativeModWindowTest]::GetWindowLongPtr($manager, -16).ToInt64() -band 0xC00000) -eq 0) { throw '管理器缺少可拖动的系统标题栏。' }
    Wait-Condition { [NativeModWindowTest]::GetWindow($manager, 4) -eq [IntPtr]::Zero } '管理器不能归属置顶宠物。'
    if (([NativeModWindowTest]::GetWindowLongPtr($manager, -20).ToInt64() -band 0x8) -ne 0) { throw '管理器不能始终置顶。' }
    if (([NativeModWindowTest]::GetWindowLongPtr($pet, -20).ToInt64() -band 0x80) -eq 0) { throw '桌宠丢失隐藏任务栏的工具窗口样式。' }

    # 实际拖动系统标题栏，不能只检查窗口声明属性。
    Start-Sleep -Milliseconds 700
    $before = [NativeModWindowTest+Rect]::new()
    [NativeModWindowTest]::GetWindowRect($manager, [ref]$before) | Out-Null
    [NativeModWindowTest]::DragTitleBar($manager, $process.Id)
    Wait-Condition {
        $after = [NativeModWindowTest+Rect]::new()
        [NativeModWindowTest]::GetWindowRect($manager, [ref]$after) | Out-Null
        $after.Left -ne $before.Left -and $after.Top -ne $before.Top
    } '拖动标题栏后窗口没有移动。'

    [NativeModWindowTest]::PostMessage($manager, 0x112, [IntPtr]0xF020, [IntPtr]0) | Out-Null
    Wait-Condition { [NativeModWindowTest]::IsIconic($manager) } '管理器未进入最小化状态。'
    $manager = Open-ManagerFromMenu $pet

    [NativeModWindowTest]::PostMessage($manager, 0x10, [IntPtr]0, [IntPtr]0) | Out-Null
    Wait-Condition { -not [NativeModWindowTest]::IsWindowVisible($manager) } '关闭管理器后窗口未隐藏。'
    $manager = Open-ManagerFromMenu $pet
    if (([NativeModWindowTest]::GetWindowLongPtr($manager, -20).ToInt64() -band 0x8) -ne 0) { throw '重新打开后管理器意外置顶。' }
    if ($process.HasExited) { throw '关闭管理器不应退出桌宠。' }
    Wait-Condition { [NativeModWindowTest]::TrayId($pet) -ge 0 } '系统托盘未注册桌宠图标。'
    $trayId = [NativeModWindowTest]::TrayId($pet)
    Click-Client $manager 584 123
    Wait-Condition {
        $statePath = Join-Path $profile 'mods-state.json'
        (Test-Path -LiteralPath $statePath) -and ((Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json).Disabled -contains 'official-mushroom')
    } '默认蘑菇不能关闭。'
    [NativeModWindowTest]::PostMessage($manager, 0x10, [IntPtr]0, [IntPtr]0) | Out-Null
    Wait-Condition { -not [NativeModWindowTest]::IsWindowVisible($manager) } '全部外观关闭后管理器未隐藏。'
    # 走 Windows 实际托盘回调入口，模拟已注册图标的左键通知。
    [NativeModWindowTest]::PostMessage($pet, 0x401, [IntPtr]$trayId, [IntPtr]0x201) | Out-Null
    Wait-Condition {
        $handle = Find-Window 'Mod 管理器'
        $handle -ne [IntPtr]::Zero -and [NativeModWindowTest]::IsWindowVisible($handle)
    } '从托盘无法恢复管理器。'
    $manager = Find-Window 'Mod 管理器'
    Click-Client $manager 584 123
    Wait-Condition {
        (Get-Content -LiteralPath (Join-Path $profile 'mods-state.json') -Raw | ConvertFrom-Json).Disabled -notcontains 'official-mushroom'
    } '从托盘恢复后无法重新开启蘑菇。'
    if (([NativeModWindowTest]::GetWindowLongPtr($pet, -20).ToInt64() -band 0x80) -eq 0) { throw '切换外观后丢失无任务栏样式。' }
    [NativeModWindowTest]::PostMessage($pet, 0x401, [IntPtr]$trayId, [IntPtr]0x204) | Out-Null
    Wait-Condition { $handle = Find-Window ''; $handle -ne [IntPtr]::Zero -and [NativeModWindowTest]::IsWindowVisible($handle) } '托盘右键菜单未显示。'
    $trayMenu = Find-Window ''
    Start-Sleep -Milliseconds 400
    $menuSize = [NativeModWindowTest]::ClientSize($trayMenu)
    [NativeModWindowTest]::ClickNativePopup($trayMenu, $process.Id, [int]($menuSize.X / 2), $menuSize.Y - 16)
    if (-not $process.WaitForExit(5000)) { throw '托盘退出命令未结束进程。' }
    if ($process.ExitCode -ne 0) { throw "退出失败：$($process.ExitCode)" }
    Write-Output 'Windows Mod manager smoke: PASS (native title-bar drag, non-topmost, switches, minimize, reopen, tray recovery/exit, taskbar)'
    Write-Output "Log: $log"
} finally {
    [NativeModWindowTest]::SetCursorPos($originalCursor.X, $originalCursor.Y) | Out-Null
    if (-not $process.HasExited) {
        $pet = Find-Window '桌面宠物模拟器 (DEBUG)'
        if ($pet -ne [IntPtr]::Zero) { [NativeModWindowTest]::PostMessage($pet, 0x10, [IntPtr]0, [IntPtr]0) | Out-Null }
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
}
