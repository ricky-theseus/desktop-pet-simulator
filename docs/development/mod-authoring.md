# Mod 开发入口

一个 Mod 至少应包含：

- 唯一的 manifest；
- 入口模块；
- 权限和能力声明；
- Core API 兼容范围；
- 安装、试跑和卸载说明；
- 最小契约测试；
- 许可证和第三方资源说明。

建议开发流程：

~~~text
创建模板
  -> 校验 manifest
  -> 检查依赖和权限
  -> 本地试跑
  -> 运行契约与集成测试
  -> 打包
  -> 用户确认
  -> 发布到 GitHub 或 Steam 创意工坊
~~~

推荐 CLI 形态：

~~~text
pet mod create <name> --kind gameplay
pet mod validate
pet mod test
pet mod run --local
pet mod pack
pet mod doctor
pet mod publish --dry-run
~~~

命令的默认输出应为结构化 JSON，用户可读说明通过单独的 human 或等价选项提供。
