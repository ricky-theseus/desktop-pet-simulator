# 宠物显示设置 v1

`IPetDisplayService` 提供独立于短时动作的显示参数，`ApiVersion` 为 `1.0`。仅在 Godot 主线程调用；从全局服务取得时作用于兼容主宠物，从 `IPetInstance.Services` 取得时只作用于该实例。玩家界面将所有宠物视为平级角色。

```csharp
var display = instance.Services.GetRequired<IPetDisplayService>();
var previous = display.GetOptions();
display.Apply(previous with { Scale = 1.5, Opacity = 0.8, AlwaysOnTop = false });
```

`PetDisplayOptions` 的字段：

| 字段 | 默认值 | 范围与作用 |
| --- | --- | --- |
| Scale | 1 | 0.5–2，角色显示大小为 64–256 个逻辑屏幕像素 |
| Opacity | 1 | 0.5–1，仅改变角色透明度，气泡与菜单仍清楚可读 |
| AlwaysOnTop | true | 角色窗口是否保持置顶 |
| SmoothScaling | false | 是否用线性采样；非像素角色可开启，像素角色保留最近邻 |

非法范围、NaN 或无限值会在改变窗口前抛出 `ArgumentOutOfRangeException`。`Apply` 同步更新角色、原生窗口大小、鼠标命中区域、气泡位置、屏幕坐标和动画挂点；以脚底中心保持桌面位置，靠近屏幕边缘时约束到可用工作区。缩小角色时仍为反馈气泡保留窗口空间。显示设置不抢占动画或活动租约，动作结束不重置显示参数。

Core 不负责某个 Mod 的设置页面或保存策略。Mod 应持久化参数，在实例创建后应用；保存失败应恢复之前的有效值，并给玩家重试入口。附加实例的位置由宿主按默认尺寸的脚底参考系保存，重新启用并应用同一缩放后不会累计偏移。

界面建议使用实时控件与“恢复默认”，保持设置窗口为可拖动的普通窗口。关闭设置窗口不关闭宠物，停止 Mod 时释放设置窗口。示例角色的设置 UI 属于 Mod，不进入最小内核。
