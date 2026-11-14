# 测试目录

- contract/：Core 公共契约、manifest、事件、权限和错误码。
- integration/：Mod 安装、加载、卸载、重载和失败隔离。
- compatibility/：Core API 与 Mod 版本兼容。
- fixtures/：可重复使用的最小 Mod、错误场景和回放数据。

当前契约测试使用 .NET 8、xUnit 和 `dotnet test`；Godot 项目使用 Godot 4.5.1 Mono 进行无头加载校验。

## Mod 管理流程

`ModManagementTests` 覆盖安装、持久化、卸载、版本/权限/路径拒绝、依赖错误、配置损坏恢复和取消。`SteamModTests` 覆盖没有 App ID 的原生 SDK 隔离。

先导入 Godot 资源并构建运行时，再在仓库根目录执行：

```powershell
./tools/pack-example-mod.ps1
$profile = Join-Path ([System.IO.Path]::GetTempPath()) ('dps-ui-' + [guid]::NewGuid())
$package = (Resolve-Path build/mods/sleepy-mushroom.zip).Path
godot --headless --path runtime --script ../tests/runtime/mod_manager_smoke.gd -- "--mod-data-dir=$profile" "--test-package=$package"
```

检查真实管理器入口、异步导入、外部 PNG 解码、切换、禁用回退、关闭窗口、重建宿主后的状态恢复和确认卸载。测试使用新的隔离目录；旧示例 ZIP 可复用，不必再次打包。

去掉 `--headless` 可检查原生窗口和绘制；追加 `--capture-dir=<已存在目录>` 保存管理器截图。生成物应放在仓库外或已忽略的构建目录。Steam 在线登录、下载回调与 macOS 导出需要另行实机验证。
