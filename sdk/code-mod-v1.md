# 外部代码 Mod SDK v1

代码 Mod 是独立编译的 .NET 8 类库，通过公开 Core 和 Godot SDK 使用宿主服务。作者不需要修改宿主、访问私有实现、引用 `Main` 或把自己的项目注册到宿主工程。可先构建宿主，之后再构建全新 ID 的 Mod，复制到 `mods/` 或导入 ZIP，重启即可发现。

## 兼容范围

| 项目 | v1 要求 |
| --- | --- |
| 包格式 / 服务 API | `formatVersion: 1` / `apiVersion: "1.0"` |
| 运行时 | .NET 8；Godot UI 使用 Godot 4.5.1 .NET |
| 编译项目 | `Godot.NET.Sdk/4.5.1`、`net8.0`、`EnableDynamicLoading=true` |
| 共享程序集 | `DesktopPet.Core.dll`、`DesktopPet.Godot.dll`、宿主自带的 `GodotSharp.dll` |
| 宿主 | 必须包含外部代码包加载能力；早期仅支持外观 ZIP 的版本不兼容 |
| 平台 | 默认不限制；原生依赖必须匹配宿主系统与架构 |

API 版本一致不代表任意 Godot 或 .NET 二进制都兼容。发布时记录实际测试的宿主版本、Godot 版本、系统与架构。不要把 Godot 编辑器、引擎程序、Core 或 SDK DLL 打进 Mod。

## 从独立仓库开始

复制 [桌面小灯示例](../examples/code-desk-lamp/README.md) 到自己的仓库，保留项目、manifest 和入口类即可；不需要子模块。更改包 ID、显示名、程序集名、命名空间及入口类型，确保 JSON 与入口类的 `Manifest` 一致。不要使用保留的 `official-` 前缀。

有两种引用方式：

1. 源码开发：项目引用公开的 `core/DesktopPet.Core/DesktopPet.Core.csproj` 和 `sdk/DesktopPet.Godot/DesktopPet.Godot.csproj`；相对路径按目录调整。
2. 完全独立：从匹配版本宿主/SDK 构建输出取得 `DesktopPet.Core.dll` 和 `DesktopPet.Godot.dll`，放在自己的 SDK 引用目录。示例项目已支持 `-p:DesktopPetSdkDirectory=<目录>`，不再访问源码项目。

独立项目的引用如下，`Private=false` 阻止共享契约被复制到包内：

```xml
<Project Sdk="Godot.NET.Sdk/4.5.1">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <GodotDisabledSourceGenerators>ScriptPathAttribute</GodotDisabledSourceGenerators>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="DesktopPet.Core" HintPath="$(DesktopPetSdkDirectory)/DesktopPet.Core.dll" Private="false" />
    <Reference Include="DesktopPet.Godot" HintPath="$(DesktopPetSdkDirectory)/DesktopPet.Godot.dll" Private="false" />
  </ItemGroup>
</Project>
```

Godot SDK 通过 NuGet 还原编译依赖，运行时使用宿主的 GodotSharp。外部类库禁用 `ScriptPathAttribute` 生成器，避免自定义 Node 子类被映射到宿主不存在的 `res://` 源码路径；其余生成器继续工作，自定义 Godot 子类仍需使用 `partial`。命名空间在 `DesktopPet.*` 下时，显式引擎类型可写 `global::Godot.FileAccess` 等，避免与 `DesktopPet.Godot` 命名空间冲突。不要引用宿主项目或其他 Mod 的内部类。

## 包格式与安装

```json
{
  "formatVersion": 1,
  "id": "example-desk-lamp",
  "name": "桌面小灯 · 代码示例",
  "description": "独立窗口和主线程更新示例。",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "kind": "code",
  "entry": "Example.DeskLamp.Mod.dll",
  "entryType": "Example.DeskLamp.LampMod",
  "dependencies": [],
  "permissions": ["full-trust"]
}
```

`entry` 为包内 DLL 相对路径；`entryType` 为实现 `DesktopPet.Core.IMod` 的公开、非抽象、无参构造类型的完整名称，不是程序集限定名。`IMod.Manifest` 的 ID、名称、版本和 API 必须与 JSON 一致。可选 `platforms` 使用宿主平台标记（`windows`、`macos`、`linux`），不匹配时包不启动。包大小、ID、路径、依赖及 ZIP 限制继承 [管理契约](mod-management-v1.md)。

```text
mods/
  example-desk-lamp/
    manifest.json
    Example.DeskLamp.Mod.dll
    MyDependency.dll           （若需要，仅此 Mod 的依赖）
    assets/                   （若需要，运行时直接加载的数据）
```

目录名称必须等于 manifest ID。目录包放在可执行文件同级 `mods/` 下；ZIP 根目录直接放 manifest 和 DLL，不能套外层目录。也可用管理器“导入 ZIP”。如果程序目录不可写，管理器可能回退到用户数据目录；以管理服务的 `ModsDirectory` 为准。旧用户数据目录中的包仍会扫描，同 ID 两处存在会报告冲突，不猜测优先级。

首次安装、更新或替换代码包需要完整退出并重启。启动时发现的有效代码包可以即时启停；运行中的程序集不支持卸载替换，不能把“停用”视为 DLL 已从进程卸载。手动替换时先退出宿主并保留备份；ZIP 导入同 ID 包会拒绝覆盖，先停用卸载旧包再导入新包，最后重启。不要同时保留两份相同 ID。

## 公共服务与生命周期

入口实现 `IMod.Start(ModContext)` 和 `Stop(ModContext)`；所有 Godot、菜单及更新服务操作都在宿主主线程执行。`context.Services.GetRequired<T>()` 获取服务；缺失会抛出异常。服务 v1 的 `ApiVersion` 为 `1.0`。

| 服务 | 用途与所有权 |
| --- | --- |
| `IModEnvironment`（Core） | `ModDirectory` 为本包资源根目录，按只读使用；`DataDirectory` 为独立可写数据目录，创建后保存数据；两者不等同于工作目录 |
| `IModUpdateService`（Core） | `Subscribe(Action<double>)` 每帧主线程回调，参数为秒；保存并释放返回的 `IDisposable`，不要阻塞回调 |
| `IModSceneService`（DesktopPet.Godot） | `Root` 为当前 Mod 专属 Godot `Node` 容器；`Present(Window)` 将独立窗口挂入容器并显示。不能释放容器本身或依赖其父节点实现 |
| `IPetMenuService`（Core） | `Register` 返回可释放命令；`Shared` 同步桌宠与托盘，窗口隐藏后可通过命令重新打开 |
| `IPetInstanceService` 等宠物服务 | 创建独立桌宠或叠加效果，按相应 [SDK 文档](README.md) 管理租约、句柄与资源 |

每包服务注册表独立，读取可回退到宿主公共服务；不要用全局静态变量跨包共享状态。宿主会在停用/启动失败时清理该包的更新订阅和场景容器，Mod 仍应主动释放自己持有的所有菜单、信号、租约、计时器、文件句柄与后台任务。`Stop` 必须容忍 `Start` 未完成以及重复调用；先解除回调，再移除窗口。关闭窗口可选择隐藏并由菜单恢复；停用则必须释放窗口。

包资源使用 `Path.Combine(environment.ModDirectory, ...)` 读取，不假设 `res://` 指向本包。运行时直接读 PNG 等数据可用绝对文件路径；需要 Godot 导入数据库的场景、纹理、音频不能仅复制原文件就声称可用，需自行验证目标发行包加载路径。首次开发建议使用基础控件或运行时解码图片，示例无外部资源依赖。

依赖程序集从本包解析，每包独立加载上下文；共享契约来自宿主。两个包不能通过各自复制的 SDK 类型互相通信。DLL 从流读取以避免文件锁，但已经加载的代码仍保持到进程结束。任意 .NET 代码都以 **full-trust** 同进程执行，权限声明不是沙箱；它不能约束恶意代码、阻止系统调用或隔离进程崩溃。只安装可信来源代码。

## 打包与验证

在公开仓库根目录使用工具；也可复制脚本到独立仓库，它不依赖宿主项目：

```powershell
./tools/pack-code-mod.ps1 -Project ./examples/code-desk-lamp/DeskLamp.Mod.csproj -Manifest ./examples/code-desk-lamp/manifest.json -OutputDirectory ./build/mods/lamp-v1
```

独立 DLL 引用构建加 `-DesktopPetSdkDirectory ./sdk-libs`。`-AssetsDirectory ./package-assets` 可选择专门整理的 JSON、PNG、Markdown 资源目录，内容复制到包根目录；不要指向整个源码仓库。工具生成 `<ID>/` 和 `<ID>.zip`，拒绝覆盖已有输出，只包含自有 DLL、选定资源和 manifest，排除共享 SDK、PDB、源码及构建元数据。第三方依赖仍需自行审查许可证和跨平台支持；工具不上传或安装包，也不验证入口运行行为。

验证时先冻结宿主构建，再单独构建新 ID 的包，复制安装后重启，确认无需重新编译宿主。按照示例 README 测试可见窗口、菜单恢复、更新回调、停用清理、再次启用及存档恢复；在隔离测试档测试缺权限、入口不存在、API 不兼容及坏包与好包并存。无网络或任务的 Mod 不需要虚构超时测试；有异步任务则要验证取消和停止等待。发布说明只列实际执行的检查和测试环境。
