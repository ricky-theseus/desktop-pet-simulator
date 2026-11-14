# Mod 管理服务与内容包 v1

当前运行时 API 为 `1.0`。管理器是 `official-mod-manager`，Steam 接入是 `official-steam`；两者通过 `ModContext.Services` 使用公共服务。Core 提供文件校验、状态保存与服务契约，不依赖 Godot 或 Steamworks。

## 首版兼容范围

本地导入接受 **外观数据包**，含 JSON、PNG 和 Markdown 说明。它们可更改形象及动作配置；不加载第三方 C#、GDScript、DLL 或 PCK。官方代码 Mod 随宿主编译，通过 `IMod.Start/Stop` 管理生命周期。manifest 的权限声明目前用于描述内置代码能力；它不是任意代码的沙箱。

ZIP 根目录必须包含 `manifest.json`，不能额外套一层文件夹：

```json
{
  "formatVersion": 1,
  "id": "example-sleepy-mushroom",
  "name": "瞌睡蘑菇",
  "description": "一个缓慢眨眼的蘑菇。",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "kind": "appearance",
  "entry": "appearance.json",
  "dependencies": [],
  "permissions": []
}
```

`entry` 指向 [外观格式 v1](appearance-v1.md)。所有路径为包内正斜杠相对路径；不支持上级目录、绝对路径或链接。外观帧由 Godot 在运行时直接解码，无需 `.import` 文件。

约束：

- ID 长度 2–80，匹配 `[a-z0-9][a-z0-9._-]{1,79}`；`official-` 前缀保留。ID 与包内路径组件不得以点或空格结尾，且不能使用 Windows 保留设备名，保证跨平台路径一致。
- 名称最多 64 字符，描述最多 2048 字符，版本使用数值版本，如 `1.0.0`。
- `dependencies` 最多 64 项，表示 Mod ID，不支持版本范围；依赖必须已安装、有效并启用，不允许自依赖或循环依赖。
- `permissions` 必须为空；`kind` 必须是 `appearance`；`apiVersion` 必须是 `1.0`。
- ZIP 最多 1024 条目，单文件最多 8 MB，总解压内容最多 64 MB；所有帧的估算 RGBA 解码内存最多 64 MB。manifest 最多 64 KB，外观 JSON 最多 1 MB。
- 安装前校验 PNG 签名和声明尺寸；应用外观时还会实际解码并检查可见像素。应用失败保留旧外观，不保存失败选择。

## 服务契约

通过 `Services.GetRequired<T>()` 获取以下接口，所有接口的 `ApiVersion` 为 `1.0`：

| 接口 | 用途 |
| --- | --- |
| `IModManagementService` | `List`、`ImportZip`、`SetEnabled`、`SelectAppearance`、`Remove`；读取安装目录及当前选择 |
| `IPetAppearanceService` | 在 Godot 主线程调用 `ApplyAppearance(path)`；`null` 恢复默认形象；加载失败保留当前画面 |
| `ISteamService` | 读取 `Status`；`GetSubscriptions` 刷新账号及订阅安装状态，不修改远端内容 |

管理服务必须串行调用。管理器将导入放到后台任务，期间禁用其他修改入口；关闭窗口可请求取消，30 秒后自动请求取消。复制阶段响应取消，最终目录移动和状态提交为不可中断的短事务。成功返回后包已完整安装；失败会清理暂存目录。没有执行外部命令或访问网络的导入步骤。

`ManagedMod.Enabled` 表示配置是否启用且有效。对内置 Mod，`Running` 表示本次会话启动时的启用状态；对外观包表示它是否为当前选择。Steam 的联网状态应读取 `ISteamService.Status`，不能从 `Running` 推断。

缺依赖的包可安装，但保持禁用并显示原因。停用或移除被其他有效、已启用 Mod 依赖的包会被拒绝。相同 ID 的新版本不会覆盖旧包；先卸载旧版本再导入。当前没有第三方代码加载、自动更新或热重载代码的协议。

## 存储与恢复

默认数据位于 Godot `user://mod-platform/`，和源码目录分离：

- `mods/<id>/`：已安装数据。
- `mods-state.json`：禁用列表和选中外观，写入临时文件后替换。
- `removed/<id>-<随机值>/`：卸载时保留的原内容，便于人工恢复；不会自动清空。
- `mods-state.json.invalid-*`：损坏配置的备份，随后使用默认状态。

默认蘑菇和管理器不能禁用或卸载。停用或卸载当前外观恢复默认蘑菇。内置 Steam Mod 的启用更改需重启生效。`--safe-mode` 暂不加载用户选择的外观及 Steam，保留管理器供恢复；`--mod-data-dir=<目录>` 可使用独立测试档，不应对同一目录同时运行多个宿主。

内容包错误使用 `ModManagementException.Code`，v1 代码前缀为 `package.*` 和 `mod.*`，包括 `package.path`、`package.permission`、`package.version`、`package.limit`、`mod.duplicate`、`mod.required`、`mod.dependency`。文件系统错误保持 .NET 的 IO 异常，取消为 `OperationCanceledException`，无效外观定义为 `ArgumentException`。界面显示原因并保持运行。

## 示例与检查

在仓库根目录运行：

```powershell
./tools/pack-example-mod.ps1
dotnet test DesktopPet.sln
```

脚本生成 `build/mods/sleepy-mushroom.zip`，默认拒绝覆盖已有 ZIP；也可用 `-OutputDirectory` 选择新的输出目录。示例复用官方帧图，只调整动作配置，不引入新的图像资产。
