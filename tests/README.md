# 测试目录

- contract/：Core 公共契约、manifest、事件、权限和错误码。
- integration/：Mod 安装、加载、卸载、重载和失败隔离。
- compatibility/：Core API 与 Mod 版本兼容。
- fixtures/：可重复使用的最小 Mod、错误场景和回放数据。

当前契约测试使用 .NET 8、xUnit 和 `dotnet test`；Godot 项目使用 Godot 4.5.1 Mono 进行无头加载校验。

## 临时帧覆盖与挂点

`PetAnimationLayerTests` 覆盖版本、相对路径、尺寸、帧数和有限挂点。独立运行时检查使用自动生成的临时 PNG，验证镜像、姿态变换、命中区域刷新、交互抢占、恢复、释放、换外观以及加载失败后继续申请：

```powershell
./tests/runtime/animation-layer/run.ps1 -GodotExecutable <Godot控制台程序> -WorkDirectory <仓库外的测试目录>
```

脚本将公共运行时构建到测试目录的 `public-build`，创建隔离项目并仅以 `--headless` 启动；不覆盖日常开发运行时或读取本地发行实现。真实 Windows 穿透裁剪仍需图形环境验证。

## Mod 管理流程

`ModManagementTests` 覆盖安装、持久化、卸载、版本/权限/路径拒绝、依赖错误、配置损坏恢复和取消。`OfflineSteamServiceTests` 验证公开版本无需平台原生 SDK 即可读取离线状态；发行实现的测试留在本地私有目录。

先导入 Godot 资源并构建运行时，再在仓库根目录执行：

```powershell
./tools/pack-example-mod.ps1
$profile = Join-Path ([System.IO.Path]::GetTempPath()) ('dps-ui-' + [guid]::NewGuid())
$package = (Resolve-Path build/mods/sleepy-mushroom.zip).Path
godot --headless --path runtime --script ../tests/runtime/mod_manager_smoke.gd -- "--mod-data-dir=$profile" "--test-package=$package"
```

检查真实管理器入口、异步导入、外部 PNG 解码、开关和外观切换、全部关闭、状态恢复、确认卸载，以及有效但全透明的 PNG 不能造成配置和画面失配。测试使用新的隔离目录；旧示例 ZIP 可复用，不必再次打包。

去掉 `--headless` 可检查原生窗口和绘制；追加 `--capture-dir=<已存在目录>` 保存管理器截图。生成物应放在仓库外或已忽略的构建目录。Steam 在线登录、下载回调与 macOS 导出需要另行实机验证。

Windows 还应运行 `./tests/runtime/windows_mod_manager_smoke.ps1 -GodotExecutable <Godot程序路径>`。此检查以隐藏参数启动独立测试进程，实际拖动系统标题栏，并验证管理器不置顶、不归属宠物、开关操作、最小化恢复、关闭后重新打开、托盘恢复与退出；所有鼠标操作限于本次测试进程的窗口，结束后恢复原光标位置。它能发现仅检查 Godot 内部属性遗漏的原生窗口问题。
