# Core / Mod / Skill 仓库初始化实施计划

> For agentic workers: 使用执行计划技能逐项实施本计划。步骤用复选框跟踪。

**Goal:** 初始化 desktop-pet-simulator 主仓库，使 Core、SDK、官方 Mod、Skill、文档和测试拥有稳定的起点，并将首个提交推送到用户授权的 GitHub 账户。

**Architecture:** 采用单主仓库起步的混合式策略。Core、SDK、官方 Mod、Skill、示例和兼容性测试先保持同版本，社区 Mod 以后通过独立仓库和 Steam 创意工坊分发。当前只建立目录边界和契约入口，不冻结具体运行时、引擎、Mod 包格式或许可证。

**Tech Stack:** Git、GitHub CLI、Markdown、PowerShell；运行时语言和游戏引擎在后续设计评审后确定。

**Spec:** docs/architecture/core-mod-skill.md、docs/development/repository-structure.md、docs/development/mod-authoring.md

## Global Constraints

- Core 只保留桌面宠物运行时和平台最小基础设施。
- Mod 是为桌面宠物增加内容或能力的运行时扩展包。
- Skill 面向玩家、开发者和 Agent，不等同于运行时 Mod。
- 公共契约、manifest、事件、权限和错误码必须版本化。
- 不提交密钥、用户数据、未脱敏资源、本机绝对路径和构建缓存。
- 提交消息使用 Conventional Commits，描述使用简体中文。
- 在许可证确定前，不把仓库宣传为已授予再分发权的正式开源发行版。

---

### Task 1: 建立仓库基础文件

Files: README.md、AGENTS.md、CONTRIBUTING.md、SECURITY.md、CODE_OF_CONDUCT.md、GOVERNANCE.md、CHANGELOG.md、.gitignore

- [ ] 验证根目录没有已有 Git 历史。
- [ ] 写入项目入口和协作规则。
- [ ] 检查入口文件全部存在。

### Task 2: 建立 Core / SDK / Mod / Skill 目录边界

Files: core/、sdk/、mods/official/slime/、skills/、examples/、tests/、tools/

- [ ] 写入目录职责和官方史莱姆 Mod 占位说明。
- [ ] 检查 Skill 入口的 YAML frontmatter。
- [ ] 检查主要目录职责文件。

### Task 3: 写入架构与仓库开发规范

Files: docs/architecture/core-mod-skill.md、docs/development/repository-structure.md、docs/development/mod-authoring.md

- [ ] 固定 Core、Mod、Skill 术语和边界。
- [ ] 固定主仓库和子仓库规则。
- [ ] 固定 Mod 创建、验证、测试、运行、打包和发布闭环。
- [ ] 扫描未处理占位符、密钥和本机绝对路径。

### Task 4: 初始化 Git、提交并验证

- [ ] 初始化 main 分支。
- [ ] 检查工作区内容。
- [ ] 使用 chore(repo): 初始化桌面宠物模拟器平台仓库 提交。
- [ ] 检查首个提交统计。

### Task 5: 创建 GitHub 远端并推送

- [ ] 确认 GitHub 身份为 ricky-theseus。
- [ ] 创建公共仓库 ricky-theseus/desktop-pet-simulator。
- [ ] 推送 main 并设置上游。
- [ ] 回查远端、默认分支和提交。

## 复核标准

- Core、SDK、官方史莱姆 Mod、Skill、文档和测试目录存在。
- README 能说明项目阶段、目录和开发入口。
- 没有提交秘密、用户数据、构建缓存或本机绝对路径。
- 许可证未被擅自选择，README 已说明当前状态。
- 首个提交消息符合 Conventional Commits。
- GitHub 远端存在并包含 main 分支。
