# 临时帧动画层 v1

`IPetAnimationService.ApiVersion` 为 `1.0`，通过 `ModContext.Services` 获取，仅在 Godot 主线程调用。该服务让 Mod 临时为当前宠物选择帧，并读取帧中命名挂点的物理屏幕位置。基础外观与动画时间线继续运行，玩家外观选择和 Mod 启停状态不变；服务不负责播放速度或玩法状态机。

## 定义文件

```json
{
  "formatVersion": 1,
  "compatibleAppearanceId": "example-pet",
  "frameSize": 64,
  "frames": {
    "pose_0": {
      "image": "frames/pose_0.png",
      "anchors": { "grip": { "x": 55, "y": 44 } }
    },
    "pose_1": {
      "image": "frames/pose_1.png",
      "anchors": { "grip": { "x": 53, "y": 42 } }
    }
  }
}
```

`compatibleAppearanceId` 对应已加载 `appearance.json` 中的 `id`，不是必须等于 Mod ID。`AppearanceId` 返回当前外观定义的 ID，无外观时为 `null`。

- 仅支持格式版本 1，1–128 个命名帧，8–512 像素的方形 PNG；每帧尺寸须等于 `frameSize`，且包含可见像素。定义文件最多 1 MiB。
- 图片路径相对于定义文件所在目录，可包含子目录；禁止绝对路径、URL、上级目录、反斜杠和目录内符号链接。全部图片验证后才安装租约。
- 每帧可有 0–64 个命名挂点，坐标以源图片左上角为原点，必须为有限值，且在 `[0, frameSize]` 内。帧名、挂点名区分大小写。
- 宿主统一按 128 像素显示，脚底中心为锚点。纹理和 alpha 轮廓加载一次后复用，切帧同步更新命中区和原生窗口穿透区域。

## 使用与生命周期

```csharp
private IPetAnimationLayer? layer;

public void BeginActivity(ModContext context, string definitionPath)
{
    var animation = context.Services.GetRequired<IPetAnimationService>();
    layer = animation.CreateLayer(Manifest.Id, definitionPath);
    layer.SetFrame("pose_0");
}

public void UpdateActivity(bool facingLeft)
{
    if (layer is null) return;
    layer.SetFrame("pose_1", mirrorX: facingLeft);
    if (layer.TryGetAnchor("grip", out var anchor))
    {
        // anchor.X、anchor.Y 可用于定位跟随挂点的窗口；未显示时收起相关界面。
    }
}

public void Stop(ModContext context)
{
    layer?.Dispose();
    layer = null;
}
```

`CreateLayer(owner, definitionPath)` 仅允许一个存活租约；即使同一 owner 重复申请也抛出 `InvalidOperationException`。owner 使用 Mod ID，长度 1–128，以 ASCII 字母或数字开头，其余字符限字母、数字、`-`、`_`、`.`；无效 owner 抛出 `ArgumentException`。申请失败不会占用租约。定义路径支持本地文件及 Godot 资源路径。

新租约尚未选帧时 `IsPresented == false`。`SetFrame(frame, mirrorX)` 选择当前帧，未知帧抛出 `ArgumentException` 并保留原帧。不兼容外观、无可见宠物或 `IPetVisualService.CanPose == false` 时，临时层隐藏，`TryGetAnchor` 返回 `false`。喂食、摸摸、拖动及菜单优先，回到可覆盖的 idle 后自动恢复已选帧。

`TryGetAnchor(name, out PetAnchorPosition)` 返回真实显示位置，含图片偏移、水平镜像、视觉姿态的位移/缩放/旋转/倾斜、宠物节点位置、视口变换与屏幕窗口位置。它区别于 `IPetScreenService` 的中立姿态坐标。挂点使用连续像素边界坐标，水平镜像为 `x → frameSize - x`。未知挂点、隐藏或释放后返回 `false`。

Mod 在活动结束、启动失败、停止或卸载时须调用 `Dispose`。释放立即恢复基础动画的当前帧，不重置时间线；重复释放无副作用，也不会影响后来申请的租约。释放后的 `SetFrame` 抛出 `ObjectDisposedException`。宿主退出时也释放仍存活的图像资源。

## 兼容性与验证

此服务是可选的新增 Core 契约，不改变既有视觉、外观和屏幕服务。持有租约本身不会取得 `IPetActivityService` 活动租约，Mod 若需要协调活动可分别申请并一并释放。

`PetAnimationLayerTests` 检查定义格式与拒绝边界；`tests/runtime/animation-layer/run.ps1` 在独立 Godot 项目中验证实际渲染状态、挂点变换、交互抢占、恢复、失败加载与租约生命周期。无头检查不验证原生 Windows 窗口最终绘制效果。
