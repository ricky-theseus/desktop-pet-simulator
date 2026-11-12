# Core

Core 目录只放桌面宠物运行时和平台最小基础设施。当前公共契约使用 .NET 8 / C#，桌面渲染由 Godot 4 .NET 承载。

新增玩法默认放入 Mod，不要直接把内容逻辑写入 Core。
Windows 和 macOS 的系统能力必须通过平台适配器接入，不能把 Win32 或 AppKit 调用写进宠物 Mod。
