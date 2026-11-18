# 宠物实例服务 v1

`IPetInstanceService.ApiVersion` 为 `1.0`，仅在 Godot 主线程调用。角色 Mod 可以创建独立的透明桌宠窗口；原有全局宠物服务继续指向主宠物，已有玩法无需修改。

## 创建与服务范围

从 `ModContext.Services` 获取工厂，调用 `Create(owner, key, appearancePath)`，返回 `IPetInstance` 生命周期租约。外观使用 [外观格式 v1](appearance-v1.md)，支持像素或非像素 PNG 帧。

- `owner` 使用 manifest ID，最长 128 字符；`key` 在 Mod 内唯一，最长 64 字符。两者以小写 ASCII 字母或数字开头，其余仅允许小写字母、数字、`-`、`_`、`.`；不接受末尾点号和 Windows 保留文件名。
- 同一 `owner/key` 存活期间不能再次创建；不同 owner 可以使用相同 key。参数、定义或帧资源错误时抛出异常，不登记半成品实例。此接口需要原生显示窗口，headless 模式抛出 `NotSupportedException`。
- `Services` 提供 `IPetAppearanceService`、`IPetVisualService`、`IPetMenuService`、`IPetScreenService`、`IPetActivityService`、`IPetAnimationService` 和 `IPetDisplayService`，版本均为 `1.0`。所有操作只作用于该实例；不能从此服务域创建其他实例或取得全局管理器。
- 实例专属菜单只出现在自己的右键菜单；从全局菜单服务注册的 `Shared` 命令出现在所有宠物及系统托盘，`TrayOnly` 只出现在托盘。各角色具有相同基础操作和“退出程序”；没有“收起这只桌宠”菜单。活动锁、动画层优先级、拖动和交互抢占在实例之间独立。
- `IPetAppearanceService.ApplyAppearance(null)` 在附加实例中恢复创建时的外观。更换某只宠物的外观不会生成新宠物。

`List()` 返回通过此服务创建的存活附加实例快照，字段为 owner、key、显示名和窗口可见状态，**不包含主宠物**，也不提供其他 Mod 的可变服务句柄。`Visible` 表示窗口是否展开；检查外观是否显示请使用实例的 `IPetVisualService.IsVisible`。

## 收起、恢复与释放

`Hide()` 收起窗口，保留租约；`Show()` 恢复同一窗口，不产生副本。附加窗口的系统关闭请求也只收起；这属于窗口生命周期行为，不是右键菜单选项。Mod 可在全局菜单注册 `TrayOnly` 恢复入口，避免收起后无法找回。任意宠物菜单的“退出程序”均结束整个宿主。在 Windows 上，首次显示和恢复后的桌宠窗口默认均不显示任务栏按钮；`--show-taskbar` 可用于调试。托盘由宿主统一提供。

宿主按 owner/key 在当前用户配置的 `pet-instances` 目录保存附加实例的位置，拖动完成、收起和释放时更新；再次创建时恢复，并根据当前显示器工作区约束坐标。无记录时优先放在主宠物旁边。存档无法读取时回退默认位置，无法写入时记录警告。

停止 Mod 或启动中途失败时，先释放动作、活动及菜单租约，再释放 `IPetInstance`。`Dispose()` 可重复调用，并关闭对应窗口；宿主退出也会销毁全部实例。`IsAlive` 可检查生命周期；释放后读取 `Services` 或调用 `Show/Hide` 抛出 `ObjectDisposedException`。已取得的服务也不能继续调用。临时隐藏不释放 Mod，动作时间线应按可见状态暂停或取消。

## 最小角色 Mod 示例

此示例由发行宿主注册。`appearancePath` 由打包层传入，不写死开发机路径；ZIP 内容包导入目前仅加载外观数据，不动态加载任意 C# Mod。

```csharp
using DesktopPet.Core;

public sealed class CompanionMod(string appearancePath) : IMod
{
    public ModManifest Manifest { get; } = new("example-companion", "伙伴", "0.1.0", "1.0");
    private IPetInstance? pet;
    private IPetMenuCommand? restore;

    public void Start(ModContext context)
    {
        if (pet is not null) return;
        var instances = context.Services.GetRequired<IPetInstanceService>();
        if (instances.ApiVersion != "1.0")
            throw new InvalidOperationException("需要 v1 宠物实例服务。");
        try
        {
            pet = instances.Create(Manifest.Id, "companion", appearancePath);
            restore = context.Services.GetRequired<IPetMenuService>()
                .Register(Manifest.Id, "show", "召回伙伴", () => pet?.Show(), PetMenuScope.TrayOnly);
            // 从 pet.Services 获取动作、菜单和位置服务，避免影响主宠物。
        }
        catch { Stop(context); throw; }
    }

    public void Stop(ModContext context)
    {
        restore?.Dispose();
        restore = null;
        pet?.Dispose();
        pet = null;
    }
}
```

## 验证与兼容性

`tests/runtime/pet-instances/run.ps1` 使用独立配置和公开蘑菇素材，在真实显示窗口验证多实例、菜单/动作隔离、连续拖动、收起恢复、位置持久化及卸载清理。通过 `-GodotExecutable` 指定 Godot 4.5.1 .NET，`-WorkDirectory` 指定仓库外的测试输出目录。Windows 验证还检查原生任务栏样式。macOS 尚未实机验证。

实例服务是进程内协作接口，不是代码 Mod 的安全沙箱；owner 标识用于生命周期和存储命名，不代表权限隔离。此版本没有跨宠物广播或把现有玩法自动应用到所有宠物；玩法需要显式持有目标实例的服务。
