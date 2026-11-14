# 宠物扩展服务 v1

三个服务的 `ApiVersion` 均为 `1.0`，仅在 Godot 主线程调用。它们提供通用菜单入口、屏幕位置和活动协调，不包含具体玩法。

## 右键入口

从 `ModContext.Services` 取得 `IPetMenuService`，调用 `Register(owner, key, text, execute)`。`owner` 使用 manifest ID，`key` 在该 Mod 内唯一；重复注册抛出异常。返回的 `IPetMenuCommand` 可更新 `Text` 与 `Enabled`，宿主同步更新菜单。禁用命令不执行，回调异常由宿主显示为可恢复提示。

```csharp
private IPetMenuCommand? command;

public void Start(ModContext context)
{
    command = context.Services.GetRequired<IPetMenuService>()
        .Register(Manifest.Id, "open", "打开活动", OpenActivity);
}

public void Stop(ModContext context)
{
    command?.Dispose();
    command = null;
}
```

`Dispose` 可重复调用；释放时移除菜单项及回调，旧句柄不会删除后来注册的同名入口。菜单不负责持久化 Mod 设置；代码 Mod 通过已有运行服务启停。

## 屏幕位置

`IPetScreenService.GetState()` 返回 `PetScreenState`：`Visible`、`CenterX`、`CenterY`、`GroundY`、`DisplaySize`。坐标与宿主屏幕窗口坐标一致，可能为负；中心和脚底以中立姿态为准，不包含视觉变形。跟随宠物的窗口应每帧重新读取位置，并在 `Visible == false` 时收起依赖宠物的界面。

## 独占活动

`IPetActivityService.TryAcquire(owner)` 在空闲时返回 `IDisposable` 租约，忙碌时返回 `null`，同一所有者也不能重复占用。持有者在结束、启动失败、停用及退出时释放。旧租约重复释放不影响新租约。

```csharp
var activities = context.Services.GetRequired<IPetActivityService>();
using var lease = activities.TryAcquire(Manifest.Id);
if (lease is null) return;
// 在租约范围内执行需要独占宠物的同步活动。
```

跨帧活动应保存租约字段并在完整生命周期结束后释放。环境类 Mod 可查询 `IsBusy` 暂停自身效果，空闲后恢复。此服务是协作协议，不是权限沙箱，也不抢占喂食、拖动等宿主交互。
