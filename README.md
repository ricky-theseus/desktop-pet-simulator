# 桌面宠物模拟器

> Desktop Pet Simulator
> 宣传语：什么都能做

桌面宠物模拟器是一个以桌面宠物为核心、以 Mod 生态为主要扩展方式的开放式桌面游戏平台。

Core 只保留宠物运行时和平台最小基础设施；外观、性格、玩法、桌面互动、Agent 接入和创意工坊能力都通过 Mod 提供。Skill 面向玩家、开发者和开发 Agent，帮助它们生成、验证和打包 Mod。

## 当前状态

仓库目前处于第一阶段实现，包含 Core / SDK / Mod / Skill 的目录边界、仓库开发规范、官方蘑菇 Mod、Agent Mod 开发 Skill 占位结构，以及 Core 契约测试入口。当前桌面渲染工程正在接入 Godot 4 .NET。

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
├── official-agent-bridge
├── official-desktop-interaction
├── official-ui-shell
└── official-workshop

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

## 许可证

当前技术路线：Godot 4 .NET / C# 负责桌面渲染和运行时宿主，Core 以 .NET 8 公共契约连接 Mod，Windows 和 macOS 系统能力通过平台适配器实现。

许可证尚未冻结。许可证确定前，仓库内容不应被宣传为已授予再分发权的正式开源发行版。
