# 瞌睡蘑菇示例

这是使用官方蘑菇原有帧图制作的独立外观内容包，通过改变待机和动作节奏展示配置能力。

在仓库根目录执行 `./tools/pack-example-mod.ps1`，再从 Mod 管理器导入生成的 `build/mods/sleepy-mushroom.zip`，点击“使用此外观”。打包时脚本从 runtime 复制所需 PNG；本目录不重复保存帧图。

兼容内容格式 v1、Core API `1.0`，无额外权限。制作规范见 [管理契约](../../sdk/mod-management-v1.md) 和 [外观格式](../../sdk/appearance-v1.md)。
