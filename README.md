# 桌面宠物模拟器

> Desktop Pet Simulator
> 宣传语：什么都能做

桌面宠物模拟器是一个以桌面宠物为核心、以 Mod 生态为主要扩展方式的开放式桌面游戏平台。

Core 只保留宠物运行时和平台最小基础设施；外观、性格、玩法、桌面互动、Agent 接入和创意工坊能力都通过 Mod 提供。Skill 面向玩家、开发者和开发 Agent，帮助它们生成、验证和打包 Mod。

## 当前状态

当前发行版本：**1.0.0**。[下载 Windows x64 版本](https://github.com/ricky-theseus/desktop-pet-simulator/releases/tag/v1.0.0)。完整解压后运行 `DesktopPetSimulator.exe`，保留同级 `mods/` 和运行库目录。

Godot 4 .NET 桌宠已支持透明窗口、动作交互与外观切换。公开版本包含默认蘑菇、Mod 管理器和离线平台服务；支持本地外观包与独立编译的 C# 代码 Mod。代码包通过可执行文件旁的 `mods/` 或 ZIP 导入安装，首次安装和更新重启生效，启动时发现的代码包可即时启停。发行接入由宿主独立加载，不属于玩家 Mod 列表。

开发者可从 [公共代码 Mod SDK](sdk/code-mod-v1.md) 与 [独立小灯示例](examples/code-desk-lamp/README.md) 开始，不必修改宿主或获取私有实现。代码包为同进程完全信任代码，不提供沙箱隔离。

<a id="一键安装-sdk-与-agent-skill"></a>

## 让 Agent 帮你制作 Mod

**复制下面这段话，发给能访问本机文件和执行命令的 AI 编程助手（例如 Codex 或 Claude Code），然后告诉它你的想法。**

```text
请读取这份《桌面宠物模拟器》官方 Agent 指南，并按指南帮我准备 Mod 创作环境：
https://github.com/ricky-theseus/desktop-pet-simulator/blob/main/AGENT_SETUP.md

接着帮我制作、验证，并安装到本机游戏里。环境检查、依赖安装、技术选择和打包都请你完成；我只需要描述想要的效果。
我想做的 Mod 是：……
```

例如：“让一只小猫在桌面追着毛线球跑，点击毛线球就换个方向。”还没想好也可以先发送前面的内容，让 Agent 准备好创作环境。

Agent 会按需准备 SDK、示例、构建工具和三个开发 Skill，在当前对话里继续制作；已有环境会优先复用。完成后告诉你从哪里打开 Mod、怎么玩，以及哪些效果已验证。玩家无需自己选择 SDK 版本、填写打包参数或执行终端命令。当前完整安装与试玩流程支持 Windows x64。

[查看 Agent 执行指南](AGENT_SETUP.md)。需要自行操作的开发者也可以展开下面的手动入口。

<details>
<summary>手动安装与打包（开发者可选）</summary>

**Windows：复制整段到 PowerShell 执行。** 默认安装 Mod 开发套件及 Codex Skill，无需预先安装 Git、Python 或 Godot 编辑器。

```powershell
& {
$ErrorActionPreference = 'Stop'
$installer = Join-Path $env:TEMP 'Install-DesktopPetModKit-1.0.0.ps1'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$release = Invoke-RestMethod 'https://api.github.com/repos/ricky-theseus/desktop-pet-simulator/releases/tags/v1.0.0' -TimeoutSec 30
$asset = $release.assets | Where-Object name -eq 'Install-ModDevKit.ps1'
if (!$asset) { throw 'Installer asset not found.' }
Invoke-WebRequest -UseBasicParsing $asset.url -Headers @{Accept='application/octet-stream'} -OutFile $installer -TimeoutSec 120
powershell -NoProfile -ExecutionPolicy Bypass -File $installer
}
```

安装后获得：

| 内容 | 用途 |
| --- | --- |
| Core / Godot SDK DLL 与接口文档 | 针对游戏 1.0.0、Mod API 1.0 独立开发，不依赖宿主源码 |
| 桌面小灯代码示例、瞌睡蘑菇外观示例 | 从可编译、可打包的例子开始修改 |
| 构建与 ZIP 打包工具 | 输出可放入游戏 `mods/` 或从管理器导入的包 |
| `desktop-pet-mod-authoring` | 指导 Agent 创建外观、玩法与独立桌宠 Mod |
| `desktop-pet-mod-debugging` | 排查加载、兼容、窗口与资源清理问题 |
| `desktop-pet-mod-publishing` | 检查包结构、兼容信息和发布准备 |

套件默认位于用户目录的 `DesktopPetModKit/`；Codex Skill 安装到 `$CODEX_HOME/skills/`，未设置时使用 `~/.codex/skills/`。安装器会校验套件 SHA-256、记录实际 SDK 路径，并编译打包桌面小灯示例，成功后打印 ZIP 位置。重新开启 Agent 会话即可发现 Skill。

如果缺少 .NET SDK 8 或更新版本，安装器会从 Microsoft 下载 .NET 8 SDK 到套件内的 `.dotnet/`；构建时从 NuGet 下载 Godot.NET.Sdk 4.5.1 等依赖。因此首次安装需要联网，不需要管理员权限，也不修改系统 PATH。游戏需[另外下载](https://github.com/ricky-theseus/desktop-pet-simulator/releases/tag/v1.0.0)。

**按需安装：** 在上面 `powershell -NoProfile ... -File $installer` 这一行末尾追加参数即可；其余命令不变。

| 需求 | 追加参数 |
| --- | --- |
| 仅 SDK、示例与工具，不注册 Skill | `-Agent None` |
| 仅配置 Skill 及其参考资料，不安装 .NET、不编译示例 | `-SkillsOnly` |
| 为 Claude Code 安装 Skill | `-Agent Claude` |
| 同时为 Codex 和 Claude Code 安装 | `-Agent Both` |
| 自定义套件位置 | `-InstallDirectory 'D:\DesktopPetModKit'` |
| 自定义 Agent 的 Skill 目录 | `-SkillsDirectory 'D:\MyAgent\skills'` |

`-SkillsOnly` 仍会下载套件中的 SDK 文档、示例与工具供 Agent 读取；之后重新执行完整安装命令即可补齐编译环境。安装器不会覆盖已有的非本工具目录，也会保留同一安装中的现有 Skill 修改。可先审阅[安装脚本](tools/install-dev-kit.ps1)，或从 [Release 下载套件 ZIP](https://github.com/ricky-theseus/desktop-pet-simulator/releases/download/v1.0.0/DesktopPetSimulator-ModDevKit-1.0.0.zip)。这一键入口目前支持 Windows。

### 开始制作第一个 Mod

安装后，在 Codex 新会话中输入：

> 使用 $desktop-pet-mod-authoring，读取已安装的 SDK 和桌面小灯示例。帮我在一个新的项目目录制作一个可开关的桌面时钟 Mod，使用公开接口，并构建为可导入游戏的 ZIP。

也可以先复制套件中的 `examples/code-desk-lamp/` 到自己的项目目录，再编译：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\DesktopPetModKit\tools\build-mod.ps1" -Project 'D:\MyMods\DeskClock\DeskLamp.Mod.csproj' -OutputDirectory 'D:\MyMods\DeskClock\dist'
```

将示例路径换成自己的项目路径；`manifest.json` 默认从项目同目录读取，也可通过 `-Manifest` 指定。输出已存在时请选择新目录。代码 Mod 首次安装或更新后需重启游戏；外观包可用套件的 `tools/pack-example-mod.ps1` 打包。

SDK、示例及原创资源遵循本项目的非商业许可；未经另行书面授权不得商用。

</details>

## 核心关系

~~~text
Core Runtime
├── 宠物运行时
├── Mod 生命周期
├── 服务注册与事件系统
├── 权限、存储、诊断和恢复
└── SDK / CLI 契约

Official Mods
├── official-mushroom
└── official-mod-manager

Skills
├── mod-authoring
├── mod-debugging
└── mod-publishing
~~~

## 仓库目录

- core/：Core 运行时和稳定契约的实现。
- sdk/：Mod schema、SDK、CLI、测试夹具和模板。
- mods/official/：随产品维护的官方 Mod。
- skills/：给 Agent 使用的 Mod 开发 Skill。
- 公开接口和开发入口文档：仅在确认适合公开后加入。
- examples/：可运行或最小化的 Mod 示例。
- tests/：契约、集成、兼容和回放测试。
- tools/：开发期工具。
- scripts/：可审计的仓库维护脚本。

## 开发入口

1. AGENTS.md
2. CONTRIBUTING.md
3. 通过维护者获取当前设计资料和架构决策

## 设计资料

产品设计、架构设计和实施计划不随公共仓库分发，保存在本地设计资料目录和受控文档中。公共仓库只保留实现所需的治理规则、接口说明和开发入口。

Steam 正式发行实现、付费与授权规则、商业策略及专属商业 Mod 保存在仓库外的本地目录，不上传云端。公开版本可独立编译和运行，不依赖私有目录。开发前执行 `git config core.hooksPath .githooks`，启用提交与推送前的目录检查；检查规则不代替内容审查。

## 许可证

当前技术路线：Godot 4.5 .NET / C# 负责桌面渲染和运行时宿主，Core 以 .NET 8 公共契约连接 Mod，Windows 和 macOS 系统能力通过平台适配器实现。

当前桌宠竖切片已经支持透明置顶窗口、像素蘑菇、点击反馈、拖动、右键菜单、喂食、居中和退出。运行说明见 `runtime/README.md`。

本项目采用 **[PolyForm Noncommercial License 1.0.0](LICENSE)**，允许许可证规定范围内的非商业使用、修改和分发，**未经另行书面授权不得商用**。这是限制商业用途的源码公开项目。原创官方资源和发行包同样遵循该授权；第三方组件保留各自许可证。详见 [授权说明](NOTICE.md)。
