# 桌宠运行时

当前运行时使用 Godot 4.5.1 Mono，窗口启用了无边框、置顶、逐像素透明和鼠标区域穿透。

## 基础交互

- 左键点击蘑菇：增加好感度并触发弹跳；按住拖动可以移动窗口。
- 右键点击蘑菇：在角色右侧打开独立菜单；右侧空间不足时切到左侧，并避开任务栏。
- 喂蘑菇：恢复饱腹度并触发开心反馈。
- 回到屏幕中央：把桌宠窗口移动到当前屏幕的可用区域中央。
- 退出桌宠：关闭运行时。

## 本地运行

安装 .NET 8 SDK 和 Godot 4.5.1 .NET 版，将对应的 Godot 可执行程序加入 PATH，以下用 `godot` 代称。首次克隆或新增素材后先完成资源导入，再构建和启动：

```powershell
godot --headless --editor --path runtime --import
dotnet build runtime\DesktopPet.Runtime.csproj
godot --path runtime
```

蘑菇素材位于 `runtime/assets/mushroom/mushroom-idle.png`，使用最近邻采样保持像素边缘。

资源旁的 `.import` 文件保存导入设置和 UID，应随素材提交；`.godot/` 是可重新生成的缓存，不提交。只构建 C# 不会执行 Godot 资源导入。

## 界面行为

- 角色下方不绘制装饰阴影。
- 点击、摸摸和喂食反馈显示在角色上方的带尾巴气泡中，字号为 20px，显示 3 秒；重复交互刷新内容和计时。
- 气泡使用独立预留空间，不覆盖角色；隐藏后释放该区域的鼠标命中范围。
- 菜单采用独立系统窗口承载 Godot UI，字号为 20px；按 Esc 或点击菜单外可关闭。
- 当前验证平台为 Windows。macOS 上的透明、置顶、输入穿透和多屏表现仍需实机验证。
