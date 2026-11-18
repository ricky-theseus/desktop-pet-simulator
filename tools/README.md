# 工具和脚本

tools/ 放开发期工具，scripts/ 放可审计的仓库维护脚本。

脚本不得静默下载可执行文件、读取密钥或修改用户数据。

Windows 开发套件的复制安装入口见 [README](../README.md#一键安装-sdk-与-agent-skill)。`install-dev-kit.ps1` 下载并校验版本固定的套件、注册 Agent Skill；缺少 .NET SDK 时明确提示并使用 Microsoft 安装脚本安装到套件内，不修改系统 PATH。支持 `-SkillsOnly`、`-Agent None/Codex/Claude/Both`、`-InstallDirectory`、`-SkillsDirectory`；`-ArchivePath` 可指定本地同版本 ZIP，仍校验内置 SHA-256。

安装后的 `build-mod.ps1` 自动引用套件内的 SDK DLL 和已检测到的 .NET SDK，接受 `-Project`、`-Manifest`、`-OutputDirectory` 和 `-AssetsDirectory`。不传项目时构建桌面小灯示例。该入口依赖安装器生成的 `install.json`，在源码仓库开发时使用下面的通用打包工具。

维护者通过 `pack-dev-kit.ps1 -SdkDirectory <匹配发行版的运行库目录> -OutputDirectory <新目录>` 生成开发套件 ZIP。仅包含公共 SDK、文档、示例、Skill、工具及原创示例素材，不包含私有实现。发布前将输出的 SHA-256 固定到安装脚本，并将脚本另存为 `Install-ModDevKit.ps1` 与 ZIP 一起上传 Release；已发布的安装版本不覆盖更新。

`pack-code-mod.ps1` 接受 `-Project`、`-Manifest` 和可选 `-OutputDirectory`，执行 Release 构建并生成目录包与 ZIP；可用 `-DesktopPetSdkDirectory` 引用匹配版本的公开 SDK DLL，用 `-AssetsDirectory` 加入专门整理的 JSON、PNG、Markdown 资源。输出已存在会拒绝覆盖，不会安装或上传。详细步骤见 [代码 Mod SDK](../sdk/code-mod-v1.md)。
