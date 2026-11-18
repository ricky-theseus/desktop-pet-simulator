# 桌面小灯：外部代码 Mod

独立 C# 类库，未在宿主项目注册或引用。窗口由 Godot 基础控件构成，没有第三方图片。可开关灯、调亮度、关闭后从共享菜单重新打开；开关状态存入本 Mod 的 `DataDirectory/lamp.txt`。

要求 .NET 8 SDK、Godot.NET.Sdk 4.5.1，以及提供 API 1.0 外部代码包能力的 Godot 4.5.1 .NET 宿主。较早只支持外观的宿主不兼容。权限为 `full-trust`：代码具有当前进程的 .NET/系统权限，没有沙箱。

通过一键安装获得开发套件时，在套件根目录执行 `./tools/build-mod.ps1`，工具会使用已安装的二进制 SDK。复制示例到自己的目录后，以 `-Project <项目.csproj>` 指定项目，默认读取同目录 `manifest.json`。完整入口见 [项目 README](https://github.com/ricky-theseus/desktop-pet-simulator#一键安装-sdk-与-agent-skill)。

从源码仓库开发时，在仓库根目录执行：

```powershell
./tools/pack-code-mod.ps1 -Project ./examples/code-desk-lamp/DeskLamp.Mod.csproj -Manifest ./examples/code-desk-lamp/manifest.json
```

输出 `build/mods/example-desk-lamp/` 和同名 ZIP。关闭宿主，把目录复制到可执行文件旁的 `mods/`，再启动；也可在管理器导入 ZIP，重启后启用。初次安装或替换 DLL 需要重启，不要把源码目录直接复制为包。

手工测试入口：

1. 使用匹配版本的游戏与 SDK 构建此包；记录宿主二进制没有变化，启动看到“桌面小灯”窗口。从源码开发时先构建宿主。
2. 观察呼吸动画，调节滑块，点击开关。关闭窗口，通过桌宠或托盘菜单“打开桌面小灯”恢复。
3. 管理器停用后，窗口和菜单消失；再次启用恢复一个窗口，动画计时归零；退出重开后保存的开关状态恢复。
4. 使用隔离测试档，分别测试删掉权限、修改 API 版本、移除入口 DLL。包应显示错误且其他包正常运行；恢复文件并重启后再启用。
5. 停用后卸载，确认菜单和窗口无残留。目录没有网络、后台任务或超时操作，取消导入由管理器处理。

`Start` 遇到服务缺失、版本不兼容或存档 IO 错误时抛出明确异常，由宿主报告并调用 `Stop`；清理支持部分初始化和重复执行。代码不处理进程级异常，不能提供安全隔离。

独立仓库使用、SDK DLL 引用和完整生命周期规则见 [代码 Mod SDK](../../sdk/code-mod-v1.md)。
