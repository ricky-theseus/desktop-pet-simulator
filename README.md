# 桌面宠物模拟器

> Desktop Pet Simulator
> 宣传语：什么都能做

桌面宠物模拟器是一个以桌面宠物为核心、以 Mod 生态为主要扩展方式的开放式桌面游戏平台。

Core 只保留宠物运行时和平台最小基础设施；外观、性格、玩法、桌面互动、Agent 接入和创意工坊能力都通过 Mod 提供。Skill 面向玩家、开发者和开发 Agent，帮助它们生成、验证和打包 Mod。

## 当前状态

当前发行版本：**1.0.0**。[下载 Windows x64 版本](https://github.com/ricky-theseus/desktop-pet-simulator/releases/tag/v1.0.0)。完整解压后运行 `DesktopPetSimulator.exe`，保留同级 `mods/` 和运行库目录。

Godot 4 .NET 桌宠已支持透明窗口、动作交互与外观切换。公开版本包含默认蘑菇、Mod 管理器和离线平台服务；支持本地外观包与独立编译的 C# 代码 Mod。代码包通过可执行文件旁的 `mods/` 或 ZIP 导入安装，首次安装和更新重启生效，启动时发现的代码包可即时启停。发行接入由宿主独立加载，不属于玩家 Mod 列表。

开发者可从 [公共代码 Mod SDK](sdk/code-mod-v1.md) 与 [独立小灯示例](examples/code-desk-lamp/README.md) 开始，不必修改宿主或获取私有实现。代码包为同进程完全信任代码，不提供沙箱隔离。

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
