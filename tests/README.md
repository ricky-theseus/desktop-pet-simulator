# 测试目录

- contract/：Core 公共契约、manifest、事件、权限和错误码。
- integration/：Mod 安装、加载、卸载、重载和失败隔离。
- compatibility/：Core API 与 Mod 版本兼容。
- fixtures/：可重复使用的最小 Mod、错误场景和回放数据。

当前契约测试使用 .NET 8、xUnit 和 `dotnet test`；Godot 项目使用 Godot 4.5.1 Mono 进行无头加载校验。
