# 工具和脚本

tools/ 放开发期工具，scripts/ 放可审计的仓库维护脚本。

脚本不得静默下载可执行文件、读取密钥或修改用户数据。

`pack-code-mod.ps1` 接受 `-Project`、`-Manifest` 和可选 `-OutputDirectory`，执行 Release 构建并生成目录包与 ZIP；可用 `-DesktopPetSdkDirectory` 引用匹配版本的公开 SDK DLL，用 `-AssetsDirectory` 加入专门整理的 JSON、PNG、Markdown 资源。输出已存在会拒绝覆盖，不会安装或上传。详细步骤见 [代码 Mod SDK](../sdk/code-mod-v1.md)。
