# 桌面宠物模拟器：Agent 创作入口

这是给编程 Agent 执行的公开指南。玩家把 README 的指令发给你后，由你准备环境、理解玩法、制作 Mod、构建、验证和本地安装。玩家负责表达想法；路径、工具、依赖和格式由你选择并处理。Skill 指导你开发 Mod，不是给桌宠运行的能力。

当前基线：游戏 **1.0.0**、Mod API **1.0**、Godot **4.5.1 .NET**、代码项目 **net8.0**。下述自动安装与桌面试玩流程适用于 **Windows x64**。

## 1. 先准备，少打断

- 简短告知“我会先准备创作环境，然后制作并测试你的 Mod”，接着执行。不要只转述本指南、给出计划或把安装命令交还玩家。
- 从对话确认玩法；若缺少想法，先准备环境，再问“你希望桌面上出现什么，或者让桌宠做什么？”。已有足够信息时直接实现可玩的版本；不让玩家决定包 ID、SDK 路径、引用方式或编译参数。
- 委托准备环境和制作 Mod 已涵盖必要的官方工具下载、本地项目创建、构建和测试，不反复申请同一授权。遵守实际 Agent 宿主的权限和用户范围，不绕过审批。收费服务、对外上传、覆盖用户项目或强制结束已有游戏不在默认范围内。
- 当前宿主需要能联网、读写本机文件、执行命令。缺少这些能力时，说明具体缺项并请玩家在支持本机操作的编程 Agent 中发送同一指令，不声称已完成安装。macOS/Linux 可继续讨论和编写源码，但不要执行 Windows 安装器，或声称已完成本平台游戏试玩。

## 2. 复用环境，按需安装

1. 检查操作系统、架构、工作目录和已有项目记录。优先检查用户目录的 `DesktopPetModKit/install.json` 及上下文已提供的套件位置，不全盘扫描。
2. 记录与本指南版本一致、文件齐全时复用。需要制作代码 Mod 而记录中没有可用 .NET SDK 时运行完整安装；仅制作 JSON/PNG 外观时用 `-SkillsOnly`，无需编译环境。只有准备环境、还未确定玩法时可先按完整安装执行。
3. 默认套件放在用户目录的 `DesktopPetModKit`。被其他内容占用、版本不符或损坏时选择新的版本目录，例如 `DesktopPetModKit-1.0.0`，仍冲突则加短后缀。不删除原目录，不让玩家处理路径冲突。
4. 根据**当前 Agent 身份**选择 `-Agent Codex` 或 `-Agent Claude`，不能仅凭磁盘存在某个目录就推断身份。其他 Agent 或无法判断时用 `-Agent None`，仍可直接读套件内的 Skill；只有用户需要同时注册两边时使用 `Both`。
5. 已有注册 Skill 属于其他安装或经过用户维护时不覆盖。可用 `-Agent None` 准备 SDK，在当前会话读取正确套件里的 Skill。只有宿主明确支持额外 Skill 目录时才使用 `-SkillsDirectory`。

### Agent 执行的 Windows 安装命令

根据上述检查调整代码块中的 `$kitRoot`、`$agentKind`、`$appearanceOnly`，然后执行整段。默认值适合通用 Agent 创建代码 Mod；这些值由你调整，**不要求玩家编辑命令**。

```powershell
& {
$ErrorActionPreference = 'Stop'
$kitRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'DesktopPetModKit'
$agentKind = 'None'
$appearanceOnly = $false
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$installer = Join-Path ([IO.Path]::GetTempPath()) ('desktop-pet-setup-' + [guid]::NewGuid().ToString('N') + '.ps1')
$release = Invoke-RestMethod 'https://api.github.com/repos/ricky-theseus/desktop-pet-simulator/releases/tags/v1.0.0' -TimeoutSec 30
$asset = $release.assets | Where-Object name -eq 'Install-ModDevKit.ps1'
if (!$asset) { throw 'Installer asset not found.' }
Invoke-WebRequest -UseBasicParsing $asset.url -Headers @{Accept='application/octet-stream'} -OutFile $installer -TimeoutSec 120
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne '552ebfef0013d96978fbe3d51ff0793d023c72fe1bdfa40764b8747412ce4b44') {
    throw 'Installer SHA-256 verification failed.'
}
$arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $installer, '-InstallDirectory', $kitRoot, '-Agent', $agentKind)
if ($appearanceOnly) { $arguments += '-SkillsOnly' }
& powershell @arguments
if ($LASTEXITCODE -ne 0) { throw 'Dev kit setup failed; inspect the error before continuing.' }
}
```

安装器从 GitHub 获取套件并校验 SHA-256；缺少编译环境时从 Microsoft 下载 .NET SDK 到套件目录，构建时还原 NuGet 依赖。无需管理员权限，不改系统 PATH。完整安装会实际构建示例并打印 ZIP 位置；检查最终结果，不能把下载成功当作环境可用。

网络失败时确认具体失败地址并合理重试；GitHub 网页/下载域名不可达时优先使用上述官方 API，不把一串诊断命令交给玩家。不要跳过校验或使用不明镜像；无法访问必要服务时说明具体阻碍并保留可恢复状态。

## 3. 在当前对话读取 Skill 并继续

**不要求玩家重新开会话，也不把“请重启 Agent”作为交付结果。** 原生 Skill 列表可能在下次会话才刷新，你现在可以用文件读取工具加载内容并按其执行。

从实际套件根目录读取：

1. `skills/desktop-pet-mod-authoring/SKILL.md`：制作与公共接口边界。
2. `skills/desktop-pet-mod-debugging/SKILL.md`：调试与运行验证。
3. `skills/desktop-pet-mod-publishing/SKILL.md`：打包与交付检查。
4. `sdk/README.md` 及玩法所需的具体接口文档、示例。

这些 Skill 的 `sdk/`、`examples/`、`tools/` 路径均相对于**实际套件根目录**。读已注册副本时同时读 `references/devkit.md`；没有该文件的套件内原始副本也可按本段使用。构建工具读取 `install.json` 中的实际 .NET 路径，不依赖全局 `dotnet`。本指南适用于已发布的 1.0.0 套件，不要求重新下载或覆盖其中的 Skill 才能采用这套流程。

在新 Mod 项目的本地说明中记录套件位置、项目入口、构建方式和玩家目标，便于后续迭代；本机记录不作为公开包内容上传。不需要克隆宿主源码、获取私有资料或安装 Godot 编辑器。

## 4. 把想法做成独立 Mod

- 仅换造型或动作：读 `sdk/appearance-v1.md` 和 `examples/sleepy-mushroom/`。新增行为、小游戏、窗口或另一只桌宠：读 `sdk/code-mod-v1.md` 和 `examples/code-desk-lamp/`。多宠共存使用宠物实例服务，不把新角色强制替换到原桌宠上。
- 优先使用用户当前的新项目目录；没有项目时在用户目录的 `DesktopPetMods/<mod-id>/` 创建。复制相关示例后再修改，保持 SDK 和原始示例可复用。
- 自行生成未占用的非保留 ID，同步 manifest、入口类 Manifest、命名空间、入口类型和程序集名，保留示例的条件 SDK DLL 引用及生成器配置。玩法只放在新 Mod 中，不改 Core 或其他 Mod 内部实现。
- 补齐交互、关闭/恢复入口、必要存档和 `Stop` 清理。独立窗口与桌宠效果使用公开服务；失败或停用后释放计时器、窗口、菜单、订阅和任务。
- 缺美术时用合法示例素材、基础图形或现有工具实现可试玩版本。造型偏好会明显影响结果时可询问；需要收费生成服务时先确认，不假定已有付费授权。

代码 Mod 使用 `tools/build-mod.ps1 -Project <新项目.csproj> -Manifest <manifest.json> -OutputDirectory <新的输出目录>`，由你填写真实路径；输出已存在就生成新的构建目录，不删除旧成果。

外观 ZIP 根目录直接放 manifest、appearance 和引用资源。`tools/pack-example-mod.ps1` 只打包自带瞌睡蘑菇示例；自定义外观应按格式收集自己的文件，不能把示例 ZIP 冒充新作品。

## 5. 验证并接入本机游戏

检查构建结果、manifest 与 ZIP 内容。代码包不带共享 SDK/GodotSharp、源码、PDB、缓存、私有资料或存档；保留合法素材和依赖所需声明。SDK、示例和原创资源遵循 PolyForm Noncommercial 1.0.0，不擅自对外发布或商用。

找游戏优先看用户提供的位置、项目记录，以及实际 `DesktopPetSimulator.exe` 进程的可执行路径，再检查已知下载位置和默认安装目录。不枚举整块磁盘或读取无关个人文件。

找不到兼容游戏且任务包括试玩/安装时，用同一个 Release API 获取 `DesktopPetSimulator-1.0.0-windows-x64.zip`，校验 SHA-256 为 `4be6112e6320f4c2762286ff0266f0774cf34cd00c12d3974065093bd3eb66d7`，完整解压到用户目录下新的 `DesktopPetSimulator-1.0.0/`。同名内容已存在时另选目录；保留 `mods/` 与运行库，不拆出单个 EXE。用户只要求制作或打包时不额外安装游戏。

运行验证使用独立包目录与数据目录：

```text
DesktopPetSimulator.exe -- --mods-dir=<本次测试包目录> --mod-data-dir=<本次测试数据目录>
```

把占位符换成绝对路径并按当前 shell 正确引用；测试包目录下是 `<mod-id>/manifest.json` 等包文件，并包含所需依赖。实际启动并检查目标效果、关闭后恢复、启停与必要存档。涉及桌面图标、输入或系统设置时，验证须在用户任务范围内，避免扰动其他工作。没有桌面观察能力时如实列出未验证的交互，不把进程存在、日志无错误或编译成功写成完整试玩通过。

验证后把目录包放入确认的游戏 `mods/<mod-id>/`，或使用实际可操作的管理器导入 ZIP。代码包首次安装、更新需完整退出再启动。游戏正在运行时先完成包；仅在用户已授权正常重启且能确保保存时重启，不强制结束进程。无法安全重启时，最后只请玩家“从托盘退出并重新打开游戏”，不把构建和安装工作推回玩家。

更新已有 Mod 时确认目标 ID 与位置，保留旧包备份和存档；没有覆盖授权时先交付新 ZIP。备份放在扫描的 `mods/` 目录之外，避免重复 ID。安装位置不可写时使用管理器实际报告的可写目录；无法确定时交付 ZIP 并说明剩余的一步导入操作，不索要管理员权限强行写入。

## 6. 面向玩家交付

先说做成了什么、在哪里打开、怎么玩，附可点击的本地产物链接；必要时补充重启游戏等剩余操作与未验证项。不要用 SDK 版本、依赖清单和构建日志淹没玩家。

只要求准备环境时，验证准备结果后问玩家想做什么即可；已描述 Mod 时继续完成它，不能在“SDK 已安装”处结束。收到新玩法要求后复用环境和项目继续迭代。
