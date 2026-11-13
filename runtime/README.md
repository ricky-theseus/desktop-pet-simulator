# 桌宠运行时

当前运行时使用 Godot 4.5.1 Mono，窗口启用了无边框、置顶、逐像素透明和鼠标区域穿透。

## 基础交互

- 左键点击蘑菇：增加好感度并触发弹跳；按住拖动可以移动窗口。
- 右键点击蘑菇：打开功能菜单。
- 喂蘑菇：恢复饱腹度并触发开心反馈。
- 回到屏幕中央：把桌宠窗口移动到主屏幕中央。
- 退出桌宠：关闭运行时。

## 本地运行

```powershell
dotnet build runtime\DesktopPet.Runtime.csproj
& 'D:\tools3\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe' --path runtime
```

蘑菇素材位于 `runtime/assets/mushroom/mushroom-idle.png`，使用最近邻采样保持像素边缘。
