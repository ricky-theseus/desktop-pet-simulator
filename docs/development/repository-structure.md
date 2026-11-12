# 仓库结构规范

## 主仓库定位

主仓库同时维护 Core、SDK、官方 Mod、Skill、示例、测试和开发文档。社区 Mod 使用独立仓库维护。

## 目录边界

- core/：只放平台内核和稳定契约实现。
- sdk/：放 schema、CLI、测试夹具、模板和开发客户端。
- mods/official/：放官方 Mod。
- skills/：放 Agent 开发指导。
- docs/：放架构、API、兼容、开发和发布说明。
- examples/：放可运行的最小示例。
- tests/：放契约、集成、兼容和回放测试。

## 子仓库规则

早期不为每个官方 Mod 建独立仓库。满足以下任一条件后再拆分：

- 需要独立版本和发布节奏；
- 维护者与 Core 团队不同；
- 资源体积明显影响主仓库；
- 需要独立安全审查；
- 需要作为第三方开发模板复用。

潜在的独立仓库包括 desktop-pet-simulator-sdk、desktop-pet-simulator-mod-template 和 desktop-pet-simulator-official-mods。

## 禁止提交

禁止提交密钥、用户数据、生成缓存、Steam 构建目录、本机绝对路径、未声明许可证的资源和未审计的可执行下载脚本。
