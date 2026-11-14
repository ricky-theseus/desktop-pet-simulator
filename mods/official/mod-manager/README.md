# official-mod-manager

内置 Mod 管理器，兼容 Core API `1.0` 和 Godot 4.5.1 .NET。实现存放在本目录，通过 runtime 项目的编译链接构建；不把界面放进 Core。

入口为宠物右键菜单 → Mod 管理器。支持搜索、导入 ZIP、打开安装目录、查看错误、启用/禁用、即时更换外观及确认卸载。独立原生窗口关闭后桌宠继续运行。

权限声明为 `mods.manage`、`ui.window`。通过 `IModManagementService`、`IPetAppearanceService`、`ISteamService` 工作，不直接调用 Steam SDK。此管理入口作为恢复手段，不能自行禁用或卸载。

数据格式、限制和存储位置见 [管理契约 v1](../../../sdk/mod-management-v1.md)。契约测试入口为 `dotnet test DesktopPet.sln`；真实 Godot UI 检查见 [测试说明](../../../tests/README.md)。
