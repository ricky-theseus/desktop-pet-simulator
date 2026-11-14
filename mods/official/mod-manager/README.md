# official-mod-manager

内置 Mod 管理器，兼容 Core API `1.0` 和 Godot 4.5.1 .NET。实现存放在本目录，通过 runtime 项目的编译链接构建；不把界面放进 Core。

入口为宠物右键菜单或系统托盘图标。界面只展示搜索、导入 ZIP 和统一 Mod 列表，不按 Steam/本地分栏。每行右侧用开关控制启停，更多菜单提供更换外观及确认卸载，错误仅在操作失败时显示。

无边框窗口通过顶部留白拖动、右下角缩放；右上角关闭按钮或 Esc 收起管理器，进程继续运行。Windows 会显式同步原生窗口的显示状态，兼容隐藏启动，且不创建独立任务栏按钮。默认蘑菇也可关闭；全部外观关闭后桌宠消失且不拦截桌面鼠标，托盘入口仍能打开管理器。完全退出请使用托盘或宠物菜单中的“退出桌宠”。

权限声明为 `mods.manage`、`ui.window`。通过 `IModManagementService`、`IPetAppearanceService` 工作，不依赖 Steam 服务。此管理入口作为恢复手段，不能自行禁用或卸载。

数据格式、限制和存储位置见 [管理契约 v1](../../../sdk/mod-management-v1.md)。契约测试入口为 `dotnet test DesktopPet.sln`；真实 Godot UI 检查见 [测试说明](../../../tests/README.md)。
