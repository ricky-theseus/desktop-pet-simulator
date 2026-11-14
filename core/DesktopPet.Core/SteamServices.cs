namespace DesktopPet.Core;

public sealed record SteamStatus(bool Available, uint AppId, string Message, string? PlayerName = null);
public sealed record WorkshopSubscription(ulong Id, bool Installed, bool NeedsUpdate, string? Directory);

/// <summary>Steam 读取服务 v1；初始化和回调由官方 Steam Mod 的生命周期驱动。</summary>
public interface ISteamService
{
    string ApiVersion { get; }
    SteamStatus Status { get; }
    IReadOnlyList<WorkshopSubscription> GetSubscriptions();
}
