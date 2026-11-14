# 桌宠运行时

当前运行时使用 Godot 4.5.1 Mono，宠物窗口启用了无边框、置顶、逐像素透明和鼠标区域穿透。管理器是独立普通窗口，使用系统标题栏拖动和窗口边框缩放，不跟随宠物置顶。

## 基础交互

- 左键点击松开：增加好感度并播放摸摸动作。按住移动至少 6px 后进入提起状态，拖动松开播放落地动作；拖动不会增加好感度。
- 右键点击蘑菇：在角色右侧打开独立菜单；右侧空间不足时切到左侧，并避开任务栏。
- 喂蘑菇：恢复饱腹度并触发开心反馈。
- Mod 管理器：在统一列表中搜索和导入外观 ZIP；每行右侧用开关控制启停，更多菜单提供换装和卸载。
- 系统托盘：点击图标打开管理器，右键可退出；关闭管理器只收起窗口。所有外观（包括默认蘑菇）均可关闭，此时桌面不显示宠物，仍可从托盘恢复。
- 回到屏幕中央：把桌宠窗口移动到当前屏幕的可用区域中央。
- 退出桌宠：关闭运行时。

## 本地运行

安装 .NET 8 SDK 和 Godot 4.5.1 .NET 版，将对应的 Godot 可执行程序加入 PATH，以下用 `godot` 代称。首次克隆或新增素材后先完成资源导入，再构建和启动：

```powershell
godot --headless --editor --path runtime --import
dotnet build runtime\DesktopPet.Runtime.csproj
godot --path runtime
```

蘑菇动作配置位于 `runtime/assets/mushroom/appearance.json`，帧图位于同目录的 `actions-v1/`，使用最近邻采样保持像素边缘。原始静态素材作为形象参考保留。

资源旁的 `.import` 文件保存导入设置和 UID，应随素材提交；`.godot/` 是可重新生成的缓存，不提交。只构建 C# 不会执行 Godot 资源导入。

## 界面行为

- 角色下方不绘制装饰阴影。
- 点击、摸摸和喂食反馈显示在角色上方的带尾巴气泡中，字号为 20px，显示 3 秒；重复交互刷新内容和计时。
- 气泡使用独立预留空间，不覆盖角色；隐藏后释放该区域的鼠标命中范围。
- 菜单采用独立系统窗口承载 Godot UI，字号为 20px；按 Esc 或点击菜单外可关闭。
- 当前验证平台为 Windows。macOS 上的透明、置顶、输入穿透和多屏表现仍需实机验证。

## 任务栏与调试

Windows 默认不显示桌宠的任务栏按钮，也不参与 Alt+Tab 切换；通过角色右键菜单退出。该行为同时用于本地预览和正式运行，适配代码隔离在 `WindowsDesktopWindow`，其他平台不会调用 Win32。需要任务栏按钮辅助调试时，使用 `godot --path runtime -- --show-taskbar`。

目前验证了 Windows 引擎运行窗口的样式和交互，尚未验证独立导出的发行包。本机需要与 Godot 4.5.1 匹配的 .NET 导出模板才能导出 C# 游戏；导出时需包含 `*.json` 外观配置，正式包应关闭额外控制台包装程序。

## 动作检查

```powershell
dotnet test DesktopPet.sln
godot --headless --path runtime --script (Resolve-Path tests/runtime/pet_interaction_smoke.gd).Path
```

去掉 `--headless` 并追加 `-- --capture-dir=<已存在的输出目录>`，可保存引擎渲染的待机、摸摸、进食、提起、落地截图。外观配置和新增形象的方法见 `sdk/appearance-v1.md`。

## Mod 管理器

右键桌宠 → Mod 管理器。运行 `./tools/pack-example-mod.ps1` 生成示例 ZIP 后，可以导入并使用瞌睡蘑菇。首版支持外观数据包，尚不导入第三方可执行代码。格式和存储说明见 [管理契约 v1](../sdk/mod-management-v1.md)。

公开构建仅提供离线平台服务。Steam 及其他发行实现不列入玩家 Mod 列表，也不读取玩家保存的发行开关。商业源码与配置由维护者保存在仓库外；构建时显式传入 `-p:PrivateDistributionTargets=<本地 targets 文件>` 接入。未传入时可独立编译；传入不存在的文件会报错，不静默回退。此路径仅用于本机构建，不保存到公开项目中。

以下参数放在 Godot 参数分隔符 `--` 之后：`--open-mod-manager` 启动时打开管理器；`--safe-mode` 跳过外部外观并请求发行实现使用安全模式；`--mod-data-dir=<目录>` 使用独立数据目录，便于隔离测试。Windows 管理窗口独立于置顶宠物；关闭管理器不退出桌宠。
