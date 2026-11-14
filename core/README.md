# Core

Core 目录只放桌面宠物运行时和平台最小基础设施。当前公共契约使用 .NET 8 / C#，桌面渲染由 Godot 4.5 .NET 承载。

新增玩法默认放入 Mod，不要直接把内容逻辑写入 Core。
Windows 和 macOS 的系统能力必须通过平台适配器接入，不能把 Win32 或 AppKit 调用写进宠物 Mod。

发行控制不属于玩家 Mod 管理服务。`ISteamService` 是只读契约；公开构建注册 `OfflineSteamService`，宿主可通过本地发行构建提供平台实现。玩家启停设置不控制发行集成。
