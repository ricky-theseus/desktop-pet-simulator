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
        $handle = Find-Window 'Mod 管理器 · 桌面宠物模拟器'
        $handle -ne [IntPtr]::Zero -and [NativeModWindowTest]::IsWindowVisible($handle) -and -not [NativeModWindowTest]::IsIconic($handle)
    } '选择菜单项后管理器没有显示。'
    return Find-Window 'Mod 管理器 · 桌面宠物模拟器'
}
try {
    Wait-Condition { (Find-Window 'Mod 管理器 · 桌面宠物模拟器') -ne [IntPtr]::Zero } '管理窗口未创建。'
    $manager = Find-Window 'Mod 管理器 · 桌面宠物模拟器'
    $pet = Find-Window '桌面宠物模拟器 (DEBUG)'
    Wait-Condition { [NativeModWindowTest]::IsWindowVisible($manager) } '管理器内部已打开，但原生窗口仍然隐藏。'
    if ([NativeModWindowTest]::GetWindow($manager, 4) -ne $pet) { throw '管理器应归属桌宠窗口，避免独立任务栏按钮。' }
    if (([NativeModWindowTest]::GetWindowLongPtr($pet, -20).ToInt64() -band 0x80) -eq 0) { throw '桌宠丢失隐藏任务栏的工具窗口样式。' }

    [NativeModWindowTest]::PostMessage($manager, 0x112, [IntPtr]0xF020, [IntPtr]0) | Out-Null
    Wait-Condition { [NativeModWindowTest]::IsIconic($manager) } '管理器未进入最小化状态。'
    $manager = Open-ManagerFromMenu $pet

    [NativeModWindowTest]::PostMessage($manager, 0x10, [IntPtr]0, [IntPtr]0) | Out-Null
    Wait-Condition { -not [NativeModWindowTest]::IsWindowVisible($manager) } '关闭管理器后窗口未隐藏。'
    $manager = Open-ManagerFromMenu $pet
    if ([NativeModWindowTest]::GetWindow($manager, 4) -ne $pet) { throw '重新打开后管理器丢失所属窗口。' }
    if ($process.HasExited) { throw '关闭管理器不应退出桌宠。' }
    Write-Output 'Windows Mod manager smoke: PASS (startup, native menu, minimize, reopen, taskbar)'
    Write-Output "Log: $log"
} finally {
    if (-not $process.HasExited) {
        $pet = Find-Window '桌面宠物模拟器 (DEBUG)'
        if ($pet -ne [IntPtr]::Zero) { [NativeModWindowTest]::PostMessage($pet, 0x10, [IntPtr]0, [IntPtr]0) | Out-Null }
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
}
