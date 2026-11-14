namespace DesktopPet.Core;

public sealed record ModManifest(string Id, string Name, string Version, string ApiVersion);

public interface IMod
{
    ModManifest Manifest { get; }

    void Start(ModContext context);

    void Stop(ModContext context);
}

/// <summary>代码 Mod 运行服务 v1；宿主启动后在同一线程串行启停，不负责保存玩家设置。</summary>
public interface IModRuntimeService
{
    string ApiVersion { get; }
    bool Contains(string id);
    bool IsRunning(string id);
    /// <summary>立即启停已注册的 Mod；重复设置不重复调用生命周期，失败时抛出包含 Mod 信息的异常。</summary>
    void SetEnabled(string id, bool enabled);
}

public interface IDesktopProvider
{
    string PlatformId { get; }
}

public sealed record PetDefinition(string AssetKey, string DisplayName);
