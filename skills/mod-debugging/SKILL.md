---
name: mod-debugging
description: 诊断桌面宠物模拟器 Mod 的导入、加载、依赖、窗口、动作、存档和启停清理问题；适用于玩家报告 Mod 无反应、启动报错或停用残留时。
---

# Mod 调试 Skill

若存在 [安装位置说明](references/devkit.md)，先读取它；SDK 文档和工具相对于该开发包目录。仓库内使用时以仓库根目录为准。

先读 `sdk/mod-management-v1.md`；代码 Mod 再读 `sdk/code-mod-v1.md`。向玩家收集错误原文、宿主/Mod/API 版本和最短复现步骤，日志仅取相关行，避免暴露薪资、个人路径或其他存档内容。

1. **安装与清单**：ZIP 根目录必须有 manifest.json；目录名等于 id，不使用 official- 前缀。核对 API 1.0、entry、entryType、platforms、依赖和权限。代码包仅声明 full-trust；外观包权限为空。重复 ID 的包不会加载。
2. **重启与依赖**：新导入代码包需重启；更新或删除已注册代码包必须完全退出程序后操作目录。确认依赖已安装、启用且实际启动成功，先修复依赖错误。
3. **编译与入口**：使用公开 SDK DLL；入口为公开无参 IMod，四项 Manifest 信息与 JSON 一致。包内不重复带 Core/GodotSharp/DesktopPet.Godot；自定义 Node 子类需关闭 ScriptPathAttribute 生成，保留其余生成器。优先用安装版 tools/build-mod.ps1 复现构建。
4. **画面与交互**：用 IModSceneService 的专属 Root 挂载窗口，调用 Present 显示。检查窗口是否被隐藏、位置是否越界、资源是否从 ModDirectory 读取；桌宠大小/显示问题参考 sdk/pet-display-v1.md 和 sdk/pet-instances-v1.md。
5. **生命周期**：检查 Stop 是否释放窗口、菜单、逐帧订阅和任务；Start 失败也会调用 Stop。释放失败时保留待清理实例供重试，不先丢引用。不要以“进程还在”代替实际运行状态。
6. **隔离回归**：使用 --mods-dir 与 --mod-data-dir 指向新的测试目录，避免改写玩家真实数据。验证问题消失、正常 Mod 不受影响、停用/再启用、重启存档。每次只改变有证据指向的原因。

报告复现条件、根因、实际修改、已执行检查与未验证范围。完整代码 Mod 是同进程信任代码，不能保证隔离任意第三方崩溃。不捏造不存在的诊断命令，也不为排查问题重置或删除玩家存档。
