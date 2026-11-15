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

`CreateLayer(owner, definitionPath)` 等价于 `CreateLayer(owner, definitionPath, priority: 0)`。`priority` 范围是 `-1000`–`1000`，数值越大越优先，超出范围抛出 `ArgumentOutOfRangeException`。不同优先级的租约可以同时存活；同一优先级只允许一个租约，即使同一 owner 重复申请也抛出 `InvalidOperationException`，旧调用连续申请两次仍会被拒绝。例如常驻背景动作可使用 `-100`，短时前景动作使用默认 `0`：

```csharp
using var background = animation.CreateLayer("example-background", backgroundPath, priority: -100);
background.SetFrame("pose_0");
using (var foreground = animation.CreateLayer("example-foreground", foregroundPath))
{
    foreground.SetFrame("pose_0");
    background.SetFrame("pose_1"); // 被遮挡期间仍可更新帧；上层释放后显示最新帧。
}
```

owner 使用 Mod ID，长度 1–128，以 ASCII 字母或数字开头，其余字符限字母、数字、`-`、`_`、`.`；无效 owner 抛出 `ArgumentException`。全部资源加载成功后才登记租约，申请失败不占用该优先级，也不影响其他层。定义路径支持本地文件及 Godot 资源路径。

新租约尚未选帧时 `IsPresented == false`。`SetFrame(frame, mirrorX)` 选择当前帧，未知帧抛出 `ArgumentException` 并保留原帧。宿主只呈现已选帧且外观兼容的最高优先级层；未选帧或不兼容的高层不会遮挡低层。被高层遮挡的租约继续保留资源和当前帧，调用方可继续推进自己的时钟并选帧，仅真正呈现的层报告 `IsPresented == true`。

不兼容外观、无可见宠物或 `IPetVisualService.CanPose == false` 时，临时层隐藏，`TryGetAnchor` 返回 `false`。喂食、摸摸、拖动及菜单优先，回到可覆盖的 idle 后自动恢复最高优先级的兼容已选帧。

`HasActiveLayer` 表示至少有一个已选帧、当前外观兼容且宠物可见的层。它不受 `CanPose` 或其他高层遮挡影响：喂食、拖动或菜单暂时抢占时仍为 `true`。环境动画可用它暂停自身动作，避免用 `IsPresented` 作暂停条件而逐帧反复抢占。没有兼容已选帧、宠物隐藏或所有层释放时为 `false`。

`TryGetAnchor(name, out PetAnchorPosition)` 返回真实显示位置，含图片偏移、水平镜像、视觉姿态的位移/缩放/旋转/倾斜、宠物节点位置、视口变换与屏幕窗口位置。它区别于 `IPetScreenService` 的中立姿态坐标。挂点使用连续像素边界坐标，水平镜像为 `x → frameSize - x`。未知挂点、隐藏或释放后返回 `false`。

Mod 在活动结束、启动失败、停止或卸载时须调用 `Dispose`。释放当前呈现层会立即恢复下一可呈现层的当前帧；没有其他可呈现层时恢复基础动画的当前帧，不重置任何时间线。释放隐藏层不影响前景层，重复释放无副作用，也不会影响后来申请的租约。释放后的 `SetFrame` 抛出 `ObjectDisposedException`。宿主退出时释放所有仍存活的图像资源。

## 兼容性与验证

此服务是可选的 Core 契约，优先级能力作为 v1 增量保持 `ApiVersion == "1.0"`，原两参数入口不变。为兼容使用更新后 Core 契约的既有服务实现与测试替身，新增接口成员有默认实现：`HasActiveLayer` 返回 `false`，三参数入口的 `0` 转发旧方法，合法非零值抛出 `NotSupportedException`。当前 Godot 宿主实现完整语义；未覆写新成员的服务仍使用上述默认行为。需要后台层的 Mod 须使用含此增量契约的宿主，并处理服务实现未支持优先级时的 `NotSupportedException`，不能仅凭版本号假定支持。

既有视觉、外观和屏幕服务不变。持有动画租约本身不会取得 `IPetActivityService` 活动租约，背景动作无需长期占用活动租约；Mod 若需要协调短时独占活动可分别申请并一并释放。

`PetAnimationLayerTests` 检查定义格式与拒绝边界；`tests/runtime/animation-layer/run.ps1` 在独立 Godot 项目中验证实际渲染状态、不同尺寸和双向镜像挂点、优先级抢占和释放恢复、隐藏层选帧、外观过滤、交互期间的活动请求、失败加载与全部租约生命周期。无头检查不验证原生 Windows 窗口最终绘制效果。
