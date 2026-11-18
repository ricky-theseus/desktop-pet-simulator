# 外部代码 Mod 桌面验收

`smoke.gd` 直接加载宿主主场景，通过真实管理器导入 ZIP、点击开关和菜单、检查独立窗口与逐帧动画。测试需要 Godot 4.5.1 .NET 图形显示，不使用 headless。

先构建宿主，记录宿主 DLL 的 SHA256。之后用已构建的 SDK DLL 独立构建 `examples/code-desk-lamp`，不再重编宿主：

```powershell
./tools/pack-code-mod.ps1 -Project examples/code-desk-lamp/DeskLamp.Mod.csproj -Manifest examples/code-desk-lamp/manifest.json -DesktopPetSdkDirectory <宿主DLL目录> -OutputDirectory <全新输出目录>
```

以可执行包或编辑器运行 `--script <smoke.gd绝对路径>`，在 `--` 后传入独立测试目录：

- 安装阶段：`--mods-dir=<空目录> --mod-data-dir=<另一个磁盘的测试档> --qa-mode=install --qa-zip=<示例ZIP> --qa-installed-manifest=<mods/example-desk-lamp/manifest.json> --qa-report=<报告>`。
- 重启阶段：相同安装目录和测试档，`--qa-mode=run --qa-state=<测试档/mod-data/example-desk-lamp/lamp.txt> --qa-report=<报告> --qa-capture=<截图>`。

两次进程均须正常退出，报告 failures 为 0，日志无意外引擎错误，宿主 DLL hash 必须保持一致。可加 `--max-fps 60 --quit-after 1800` 防止测试意外停滞；超时退出而没有报告视为失败。

额外放入 API 不兼容、重复 ID 或损坏 DLL 包，确认管理器仍可显示、有效包仍能运行。测试只能使用隔离目录，不操作玩家存档或正在使用的安装目录。
