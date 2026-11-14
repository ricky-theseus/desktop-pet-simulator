# Mod 管理服务与内容包 v1

当前运行时 API 为 `1.0`。管理器是 `official-mod-manager`，通过 `ModContext.Services` 使用公共服务。Core 提供文件校验、状态保存与服务契约，不依赖 Godot 或 Steamworks。管理器只列出玩家 Mod；Steam 等发行集成由宿主独立加载，不向管理服务登记，玩家不能搜索、启停或卸载这些集成。公开构建仅注册离线平台服务。

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
| `IModRuntimeService` | `Contains`、`IsRunning`、`SetEnabled`；查询宿主已注册的代码 Mod，并立即调用其启动或停止生命周期 |
| `IPetAppearanceService` | 在 Godot 主线程调用 `ApplyAppearance(path)`；`null` 恢复默认形象；加载失败保留当前画面。`ClearAppearance()` 清除外观并释放资源，管理入口继续可用 |
| `IPetVisualService` | 在宿主主线程读取显示与交互状态，并设置有范围限制的姿态；详见 [视觉服务 v1](pet-visual-v1.md) |
| `ISteamService` | 读取 `Status`；`GetSubscriptions` 刷新账号及订阅安装状态，不修改远端内容 |

管理服务必须串行调用。管理器将导入放到后台任务，期间禁用其他修改入口；关闭窗口可请求取消，30 秒后自动请求取消。复制阶段响应取消，最终目录移动和状态提交为不可中断的短事务。成功返回后包已完整安装；失败会清理暂存目录。没有执行外部命令或访问网络的导入步骤。

`ManagedMod.Enabled` 表示配置是否启用且有效。`BuiltInMod.AppearancePath` 可声明内置外观，和本地外观使用相同选择规则。给 `ModManagementService` 传入可选的 `runtime` 参数后，代码 Mod 的 `Running` 从该服务读取实际运行状态；未传入时保留启动配置快照的兼容行为。对外观（包括内置外观），`Running` 仍表示它是否为当前选择。Steam 的联网状态应读取 `ISteamService.Status`，不能从 `Running` 推断。

`ModHost` 实现 `IModRuntimeService`。宿主先注册代码 Mod，再创建管理服务并调用 `StartAll`；可传入 `Func<string, bool>` 根据持久设置选择启动项，例如 `host.StartAll(id => management.List().Any(mod => mod.Id == id && mod.Enabled))`。不传谓词则启动全部已注册项；重复调用 `StartAll` 不重复启动。发行集成不应依赖玩家管理设置，宿主需单独处理这些集成。

运行期间，`SetEnabled(id, true/false)` 立即调用 `Start/Stop`，同一状态的重复操作不重复调用生命周期。`Contains` 和 `IsRunning` 对未知 ID 返回 `false`；切换未知 ID 或在宿主启动前切换会抛出可读异常。所有生命周期调用须在宿主线程串行执行；服务本身不提供并发调度。`Start` 失败时宿主调用该 Mod 的 `Stop` 清理部分初始化资源，不将其标记为运行；启动和清理均失败时同时报告两项错误，并保留待清理记录。后续关闭、`StopAll` 或重新开启会先重试清理，成功后才允许再次 `Start`。此时此前成功启动的 Mod 仍受宿主管理。

`StopAll` 按注册逆序尝试停止所有运行项及待清理项，某一项失败不会跳过其他项，最后聚合报告错误。停止失败的项保留运行或待清理标记，允许重试停止；全部停止成功后可再次 `StartAll`。Mod 的 `Stop` 必须能处理 `Start` 尚未完成的情况，并应允许失败后的重复清理。

管理服务切换已注册 Mod 时先执行生命周期，再保存设置；启动或停止失败不写入新设置。保存失败会尝试恢复原运行状态，恢复成功后继续抛出原文件系统异常；恢复也失败时返回 `mod.rollback`，同时说明保存和恢复错误。此时设置仍保持旧值，调用方应结合 `Running` 提示用户处理实际运行状态。单独使用 `IModRuntimeService` 不会保存玩家设置。

`SetEnabled(id, true)` 启用外观时，同时原子保存为当前选择。关闭或移除当前外观后，选择列表中第一个可用且已启用的外观；没有可用外观时，`SelectedAppearanceId/Path` 返回 `null`。管理器在保存开关/选择前实际加载候选外观；解码失败保留原配置和画面，保存失败恢复原画面。全关闭时导入的首个外观若不能显示，仍保留安装记录，但自动关闭该包并显示原因。

缺依赖的包可安装，但保持禁用并显示原因。停用或移除被其他有效、已启用 Mod 依赖的包会被拒绝。相同 ID 的新版本不会覆盖旧包；先卸载旧版本再导入。即时启停只作用于宿主预先注册的代码 Mod；当前没有第三方程序集加载、自动更新或替换运行中代码的协议。

## 存储与恢复

默认数据位于 Godot `user://mod-platform/`，和源码目录分离：

- `mods/<id>/`：已安装数据。
- `mods-state.json`：禁用列表和选中外观，写入临时文件后替换。
- `removed/<id>-<随机值>/`：卸载时保留的原内容，便于人工恢复；不会自动清空。
- `mods-state.json.invalid-*`：损坏配置的备份，随后使用默认状态。

管理器默认开启，不能自行关闭或卸载；列表内其他官方 Mod 均可关闭。内置文件随应用发布，不支持从界面删除。默认蘑菇是可选外观，不会在全部外观关闭后强制恢复。宿主此时停止渲染宠物并穿透鼠标，托盘仍可打开管理器；关闭管理窗口只收起，托盘菜单“退出桌宠”才终止进程。旧配置中的 `official-steam` 禁用记录不再控制发行集成。`--safe-mode` 临时显示默认外观并请求发行实现使用安全模式，保留管理器供恢复，不改写保存状态；`--mod-data-dir=<目录>` 可使用独立测试档，不应对同一目录同时运行多个宿主。

内容包错误使用 `ModManagementException.Code`，v1 代码前缀为 `package.*` 和 `mod.*`，包括 `package.path`、`package.permission`、`package.version`、`package.limit`、`mod.duplicate`、`mod.required`、`mod.dependency`、`mod.lifecycle` 和 `mod.rollback`。文件系统错误保持 .NET 的 IO 异常，取消为 `OperationCanceledException`，无效外观定义为 `ArgumentException`。界面显示原因并保持运行。

## 示例与检查

在仓库根目录运行：

```powershell
./tools/pack-example-mod.ps1
dotnet test DesktopPet.sln
```

脚本生成 `build/mods/sleepy-mushroom.zip`，默认拒绝覆盖已有 ZIP；也可用 `-OutputDirectory` 选择新的输出目录。示例复用官方帧图，只调整动作配置，不引入新的图像资产。
