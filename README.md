# 桌面宠物模拟器

> Desktop Pet Simulator
> 宣传语：什么都能做

桌面宠物模拟器是一个以桌面宠物为核心、以 Mod 生态为主要扩展方式的开放式桌面游戏平台。

Core 只保留宠物运行时和平台最小基础设施；外观、性格、玩法、桌面互动、Agent 接入和创意工坊能力都通过 Mod 提供。Skill 面向玩家、开发者和开发 Agent，帮助它们生成、验证和打包 Mod。

## 当前状态

仓库目前处于初始化阶段，包含 Core / SDK / Mod / Skill 的目录边界、仓库开发规范、官方史莱姆 Mod 占位目录、Agent Mod 开发 Skill 占位结构，以及后续实现所需的文档、测试和工具入口。

当前尚未提供可运行的游戏版本，也尚未冻结具体引擎、语言、Mod 包格式或开源许可证。

## 核心关系

~~~text
Core Runtime
├── 宠物运行时
├── Mod 生命周期
├── 服务注册与事件系统
├── 权限、存储、诊断和恢复
└── SDK / CLI 契约

Official Mods
├── official-slime
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
- docs/：架构、API、开发、兼容和发布文档。
- examples/：可运行或最小化的 Mod 示例。
- tests/：契约、集成、兼容和回放测试。
- tools/：开发期工具。
- scripts/：可审计的仓库维护脚本。

## 开发入口

1. AGENTS.md
2. CONTRIBUTING.md
3. docs/architecture/core-mod-skill.md
4. docs/development/repository-structure.md
5. docs/development/mod-authoring.md

## 许可证

许可证尚未冻结。许可证确定前，仓库内容不应被宣传为已授予再分发权的正式开源发行版。
