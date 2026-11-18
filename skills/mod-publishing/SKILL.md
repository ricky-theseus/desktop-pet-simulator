---
name: mod-publishing
description: Use when 为桌面宠物模拟器整理、验证、打包或发布 appearance/code Mod，或准备本地目录包与 ZIP 安装说明。
---

# Mod 发布 Skill

若存在 [安装位置说明](references/devkit.md)，先读取它；后文相对路径以其中的开发包根目录为准。源码仓库内使用时，以仓库根目录为准。

先读 `sdk/mod-management-v1.md`；代码包还需读 `sdk/code-mod-v1.md`。使用公开契约和示例即可，不要求私有设计、商业代码或素材。

## 本地预演

1. 校对 manifest：唯一 ID、数值版本、API 1.0、入口路径、依赖和目标平台。appearance 权限为空，code 必须仅声明 `full-trust` 并提供 `entryType`。入口 `IMod.Manifest` 必须与 JSON 一致。
2. 审查素材与第三方依赖授权；不编造许可证，不打包用户存档、密钥、源码、私有实现或设计资料。
3. 安装版 code 使用 `tools/build-mod.ps1 -Project <csproj> -Manifest <manifest.json> -OutputDirectory <新目录>`；源码仓库使用 `tools/pack-code-mod.ps1`，可用 `-DesktopPetSdkDirectory` 独立引用 SDK DLL。输出目录已存在时选择新目录，不强制删除或覆盖。只允许自有 DLL、包资源与 manifest，排除共享 Core/Godot SDK 与 GodotSharp。
4. 检查 ZIP 根目录直接包含 manifest；目录包名称等于 ID。清点实际文件，检查路径/大小限制。工具只构建本地包，不上传、不安装，不能把打包成功当作运行验证通过。
5. 在隔离测试档测试新安装、重启发现、启用、停用清理、再次启用、卸载和错误隔离。至少包含缺权限、入口缺失、API 不兼容；有异步任务才测试超时/取消。发布说明写实际环境和结果，未执行项标明未执行。

## 安装和发布说明

代码目录包放到可执行文件同级 `mods/<id>/`，或在管理器导入 ZIP，重启后启用。首次安装/更新需重启；不能承诺停用后可热替换 DLL。相同 ID ZIP 不覆盖旧包，更新或删除已注册代码包须完全退出程序后操作目录，保留存档；不能把“关闭开关”当作卸载程序集。外观包继续按外观流程即时切换。

代码包是任意 .NET 完全信任代码，同进程无沙箱。说明权限、兼容版本、系统/架构、依赖与来源，不伪称签名或安全审查。

本地预演、打包不授权远端发布。实际上传、创意工坊发布或对外分发必须有对应用户授权；已有授权不重复索取。没有接入发布工具时交付本地可审查包和说明，不虚构上传结果。
