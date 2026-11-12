# 贡献指南

## 开始之前

先阅读：

- AGENTS.md
- docs/architecture/core-mod-skill.md
- docs/development/repository-structure.md
- docs/development/mod-authoring.md

如果修改公共 API、manifest、事件或权限模型，先提交设计说明，再开始实现。

## 变更类型

- core：Core 运行时、公共契约或恢复机制。
- sdk：schema、CLI、SDK、模板和测试夹具。
- mod：官方 Mod 或 Mod 示例。
- skill：Agent 开发 Skill 和参考资料。
- docs：架构、开发和发布文档。
- build：构建、CI 或仓库维护。

## Pull Request 要求

PR 描述至少说明：

1. 解决的问题和用户影响；
2. 变更的目录和公共接口；
3. 是否影响现有 Mod；
4. 已运行的验证命令和真实结果；
5. 失败时如何回滚或禁用。

公共 API 变更必须同时提交兼容性说明、迁移或弃用策略、契约测试和 CHANGELOG.md 更新。

## Mod 贡献

社区 Mod 应独立维护源码仓库，再通过版本化包或创意工坊分发。不要把所有社区 Mod 汇总进官方仓库。
