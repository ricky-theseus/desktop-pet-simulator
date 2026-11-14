# 宠物视觉服务 v1

`IPetVisualService.ApiVersion` 为 `1.0`。宿主通过 `ModContext.Services` 提供此服务，供已注册代码 Mod 调整当前宠物的视觉姿态。它不改变外观文件、动画配置或桌面窗口位置。

| 成员 | 含义 |
| --- | --- |
| `IsVisible` | 当前是否显示宠物；所有外观关闭时为 `false` |
| `CanPose` | 当前是否允许附加姿态；宠物可见、处于待机且没有按下指针或展开宠物菜单时为 `true` |
| `SetPose(PetVisualPose pose)` | 在宿主主线程设置姿态，同时更新鼠标命中区域；无效输入抛出 `ArgumentOutOfRangeException` |

`PetVisualPose(X, Y, ScaleX, ScaleY, Rotation, Skew)` 使用脚底作为变形原点。`X/Y` 按宠物显示高度归一；当前宿主显示高度为 128 个画布单位，因此 `X = 0.1f` 表示向右偏移 12.8 个画布单位。`Rotation/Skew` 使用弧度，缩放为相对正常外观的比例。

`IsValid` 检查以下范围，所有值均须为有限数：

| 字段 | 允许范围 |
| --- | --- |
| `X`、`Y` | `[-0.3, 0.3]` |
| `ScaleX`、`ScaleY` | `[0.5, 1.5]` |
| `Rotation`、`Skew` | `[-0.5, 0.5]` 弧度 |

`PetVisualPose.Neutral` 为 `(0, 0, 1, 1, 0, 0)`。不要使用全零的默认结构体作为复位值，其缩放不符合契约。

用户交互优先于附加姿态。`CanPose` 为 `false` 时，宿主将合法的姿态请求按 `Neutral` 处理；切换外观、清除外观和播放交互动作也会复位。调用方不应通过重复设置姿态覆盖拖动、喂食或摸摸动作。

```csharp
var visual = context.Services.GetRequired<IPetVisualService>();
if (visual.CanPose)
    visual.SetPose(new PetVisualPose(0, 0, 1, 1, 0.1f, 0));

// 在 Mod.Stop 中复位，并释放该 Mod 自己注册的更新回调。
visual.SetPose(PetVisualPose.Neutral);
```

本服务不创建计时器或后台任务；代码 Mod 自己拥有的更新入口必须与 `Start/Stop` 生命周期绑定，且只在宿主主线程调用视觉服务。
