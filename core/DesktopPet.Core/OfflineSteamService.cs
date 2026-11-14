namespace DesktopPet.Core;

/// <summary>公开构建的离线服务；不初始化平台 SDK，不提供发行控制能力。</summary>
public sealed class OfflineSteamService : ISteamService
{
    public string ApiVersion => "1.0";
    public SteamStatus Status { get; } = new(false, 0, "当前为离线构建。");
    public IReadOnlyList<WorkshopSubscription> GetSubscriptions() => [];
}
