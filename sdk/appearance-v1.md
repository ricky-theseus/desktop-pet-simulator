# 外观与动作格式 v1

这是当前像素帧动画显示入口的格式说明。Core 提供 `PetAppearanceDefinition` 校验和 `PetAnimationTimeline` 播放时序；Godot 的 `PetView` 加载素材并显示。具体形象、动作帧和动作节奏由外观内容提供，饱腹度、好感度等玩法仍在官方 Mod 中。

## 配置入口

在 Godot 项目配置的 `[desktop_pet]` 节设置 `appearance="res://assets/形象目录/appearance.json"`。切换此项即可复用同一个显示器和菜单，无需编写新的角色类。当前是随项目打包的内容入口，尚未实现用户 Mod 目录发现、创意工坊下载或运行中切换外观。

```json
{
  "formatVersion": 1,
  "id": "example-pet",
  "displayName": "示例宠物",
  "frameSize": 64,
  "frames": ["frames/idle.png", "frames/blink.png"],
  "actions": {
    "idle": { "frames": [0, 0, 0, 0, 1], "fps": 2, "loop": true }
  }
}
```

## 字段与兼容规则

- `formatVersion` 当前只接受整数 `1`；不支持的版本在加载时拒绝。
- `id` 和 `displayName` 必填。菜单使用显示名，动作规则不依赖名称。
- `frameSize` 为 8–512 之间的整数；每张图片必须为该尺寸的正方形 RGBA PNG。同一角色所有帧使用同一比例，以底部中心为锚点；当前显示尺寸统一为 128×128。
- `frames` 为相对于配置文件目录的 PNG 路径列表；不接受绝对路径、URL、反斜杠、`.` 或 `..` 路径段。Godot 导入后的纹理也必须打包。
- `actions` 是动作名到帧序列的映射。每个序列的 `frames` 使用从 0 开始的索引，`fps` 大于 0 且不超过 60，`loop` 控制是否循环。重复索引可延长某个姿势的停留。
- `idle` 是唯一必需动作，必须循环。未知动作回退到 `idle`，单次动作播放结束后回到 `idle`，后来的动作请求打断当前动作。可以定义更多动作名，由玩法按需请求。
- 校验失败抛出 `ArgumentException`；无效时间步长抛出 `ArgumentOutOfRangeException`。配置验证后应视为只读，不在播放期间修改数组或字典。

## 当前宿主请求的动作

| 动作名 | 触发 | 推荐素材 |
| --- | --- | --- |
| `idle` | 默认与单次动作结束 | 待机、呼吸、眨眼 |
| `pet` | 点击松开或菜单摸摸 | 笑脸、举手 |
| `eat` | 菜单喂食 | 取食、咀嚼 |
| `held` | 按住后移动至少 6px | 悬腿、摆动；建议循环 |
| `land` | 拖动松开或失去焦点 | 落地压缩、恢复 |

蘑菇完整示例位于 `runtime/assets/mushroom/appearance.json`。同体型形象可以复用动作节奏和制作模板，不同体型通常需要自己的帧图。当前 v1 将动作映射放在外观配置中，尚不支持把独立动作包任意叠加到其他外观上。

## 素材与分发检查

检查帧尺寸、统一调色板、轮廓稳定、透明背景和脚底锚点；在实际 128px 显示尺寸下检查表情。导出游戏时应包含 `*.json` 配置以及全部帧图，不要仅导出主场景依赖。生成缓存仍不提交，素材旁的 `.import` 设置随素材提交。

测试入口：`dotnet test DesktopPet.sln`；引擎输入与动作衔接检查见 `tests/runtime/pet_interaction_smoke.gd`。
