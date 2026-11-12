# 桌面宠物模拟器平台架构

本目录保存 Core、Mod、Skill、仓库和发布链路的设计依据。

当前设计原则：

- Core 只保留桌面宠物运行时和平台最小基础设施。
- 所有可替换内容和玩法都通过 Mod 提供。
- Skill 面向 Agent，帮助 Agent 生成和验证 Mod。
- Service Definition、Provider、Consumer 分离。
- 公共契约拥有唯一来源、版本和兼容策略。
- 任何副作用都必须有权限、取消、诊断和恢复路径。

详细开发规范见：

- docs/development/repository-structure.md
- docs/development/mod-authoring.md
- 飞书架构规范文档：https://mcn1xzuutckh.feishu.cn/docx/HJQMdhIWCoJygixTYTvcHwLsnKe
