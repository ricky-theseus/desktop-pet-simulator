namespace DesktopPet.Core;

public sealed record SteamStatus(bool Available, uint AppId, string Message, string? PlayerName = null);
public sealed record WorkshopSubscription(ulong Id, bool Installed, bool NeedsUpdate, string? Directory);

/// <summary>Steam 读取服务 v1；初始化和回调由宿主发行集成负责，不属于玩家 Mod 开关。</summary>
public interface ISteamService
{
    string ApiVersion { get; }
    SteamStatus Status { get; }
    IReadOnlyList<WorkshopSubscription> GetSubscriptions();
}
