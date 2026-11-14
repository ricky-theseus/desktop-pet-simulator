# official-steam

内置 Steam 集成 Mod，兼容 Core API `1.0`、.NET 8。固定依赖 Steamworks.NET `2024.8.0`（对应 Steamworks SDK 1.60），通过 `ISteamService` 提供只读状态和创意工坊订阅列表。

已实现：

- 生命周期初始化、每帧回调、停止时关闭。
- 校验运行中的 App ID 与项目配置一致，读取登录状态和昵称。
- 查询订阅 ID、安装目录，以及未下载、等待更新、已安装状态。
- 由用户点击导入已下载订阅中的 `mod.zip`，经过与本地 ZIP 相同的验证；需要更新的项目先等待 Steam 完成更新。

权限声明为 `steam.read`、`workshop.read`。本版不提交工坊作品、不主动请求下载、不修改成就或云存档。工坊浏览按钮由用户点击后打开系统浏览器，不依赖透明小窗口的 Steam Overlay。

## 无 App ID

`runtime/project.godot` 中 `desktop_pet/steam_app_id=0`。该值为 0 时不会调用原生 Steam SDK；管理器、换装和本地存储均可离线使用。不要用其他应用的 ID 充当已完成接入的验证。

## 有正式 App ID 后

1. 将 `desktop_pet/steam_app_id` 改为本应用 ID；通过 Steam 启动，或按 Valve 文档在开发进程工作目录提供本地 `steam_appid.txt`。该文件已忽略，不随正式包发布。
2. 从已获授权的 Steamworks SDK **1.60** 提供匹配架构的原生库：Windows x64 为 `steam_api64.dll`；macOS 为 `libsteam_api.dylib`。NuGet 包提供托管封装，不包含这些原生库。
3. 使用 `dotnet build runtime/DesktopPet.Runtime.csproj -p:SteamworksNativeLibrary=<原生库文件>` 复制到程序集输出目录；发行构建也需确认该库随包发布且可加载。macOS 还需完成签名、平台测试和相应导出配置。
4. 使用有应用权限的 Steam 账号启动客户端。确认管理器显示昵称和连接状态，再测试订阅、下载、更新及安装导入。

原生库缺失、架构错误、版本入口不匹配或初始化失败会显示离线原因，本地内容继续可用。不同 SDK 版本的库不要未经验证混用。本仓库不包含 Steam 原生二进制和账号凭据。

工坊内容约定：每个 Workshop 项目根目录放 `mod.zip`，其内容满足 [外观内容包 v1](../../../sdk/mod-management-v1.md)。导入是本地副本，取消订阅不会自动删除已导入内容。包更新暂按卸载旧版再导入新版处理。

## 验证边界

`SteamModTests` 验证未配置 App ID 时的启动、重复启动、回调、空订阅和停止均无需原生 SDK。实际登录、订阅下载以及 macOS 原生库尚需正式 App ID 和目标机器联调。

接口依据：[Steamworks API](https://partner.steamgames.com/doc/sdk/api)、[Workshop 实现指南](https://partner.steamgames.com/doc/features/workshop/implementation)、[Steamworks.NET 安装说明](https://steamworks.github.io/installation/)。
