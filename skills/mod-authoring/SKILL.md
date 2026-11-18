---
name: mod-authoring
description: Use when 为桌面宠物模拟器创建或修改外观、独立 C# 代码 Mod、玩法、桌面互动或系统集成。
---

# Mod 开发 Skill

先阅读公开契约，外部作者无需私有设计文档或宿主源码：

- `sdk/README.md`、`sdk/mod-management-v1.md`
- 代码 Mod：`sdk/code-mod-v1.md`、`examples/code-desk-lamp/`
- 外观：`sdk/appearance-v1.md`、`examples/sleepy-mushroom/`

## 选择与边界

仅改外观时使用 appearance JSON/PNG 包；新增行为、玩法、窗口或服务集成可使用独立 code DLL 包，不要误判为只能编写外观。只依赖公开 Core/Godot SDK、事件和服务，不引用宿主 `Main`、其他 Mod 内部类型或商业实现，不要求将新玩法写进 Core 或宿主工程。

代码项目使用 .NET 8 与 Godot.NET.Sdk 4.5.1；独立仓库可用 SDK DLL 引用，不强制子模块。入口为公开无参 `IMod`，JSON 与入口 `Manifest` 一致，声明 `full-trust`。同进程执行没有沙箱，不把权限字段描述为安全隔离。

## 交付流程

1. 明确包 ID、API 1.0、版本、目标平台和来源；不使用保留 ID 或未经授权的图片。
2. 按小灯示例构建独立类库。通过 `IModEnvironment` 读包资源、写独立存档，通过 `IModUpdateService` 订阅主线程更新，通过 `IModSceneService` 创建独立窗口。需要桌宠时读对应公共服务文档。
3. `Stop` 释放菜单、订阅、窗口、信号和后台任务，容忍初始化失败及重复清理。窗口隐藏后提供可恢复入口。
4. 用 `tools/pack-code-mod.ps1` 生成目录包/ZIP；不包含共享 SDK、GodotSharp、源码、PDB、私有资料或存档。外观沿用外观打包工具。
5. 先构建宿主，再单独构建新 ID 包，验证目录安装/ZIP 导入、重启发现、可见功能、启停清理、重启存档及坏包隔离。缺权限、目标文件不存在、API 不兼容和卸载必须覆盖；有异步任务时再覆盖超时/取消。

生成结果包含 manifest、权限声明、Core/Godot 兼容范围、测试入口、实际验证结果和失败降级说明。代码包首次安装/更新需重启，停用不卸载已加载程序集；不要承诺热替换 DLL。发布或上传需要用户授权；本地编写与打包本身不等于发布授权。
