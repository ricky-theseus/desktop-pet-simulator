# SDK

玩家和外部开发者可使用 [Windows 一键安装](../README.md#一键安装-sdk-与-agent-skill)，获取匹配发行版的 SDK DLL、接口文档、示例、打包工具和 Agent Skill。安装后的 `tools/build-mod.ps1` 使用二进制 SDK 引用，不需要克隆宿主工程。

SDK 目录将承载 Mod manifest schema、CLI、测试夹具、模板和 Agent 使用的结构化接口。

SDK 的公共契约必须与 Core API 版本同步，并提供兼容矩阵。

独立 C# 类库、目录/ZIP 安装、服务生命周期与打包步骤见 [外部代码 Mod SDK v1](code-mod-v1.md)。可直接构建 [桌面小灯示例](../examples/code-desk-lamp/README.md)，不需要修改宿主工程。

已实现的外观帧动画配置、动作回退与制作规范见 [外观与动作格式 v1](appearance-v1.md)。

本地安装包、管理服务、兼容性与恢复规则见 [Mod 管理契约 v1](mod-management-v1.md)。

当前姿态、变形范围与交互优先规则见 [宠物视觉服务 v1](pet-visual-v1.md)。

右键菜单命令、屏幕位置和独占活动协调见 [宠物扩展服务 v1](pet-extension-v1.md)。

临时帧覆盖、镜像挂点与资源租约见 [临时帧动画层 v1](pet-animation-layer-v1.md)。

多只桌宠共存、独立服务域、收起恢复与角色 Mod 示例见 [宠物实例服务 v1](pet-instances-v1.md)。

持续大小、透明度、置顶与采样方式见 [宠物显示设置 v1](pet-display-v1.md)。
