namespace DesktopPet.Core;

/// <summary>宠物实例服务 v1；主线程调用。全局旧服务仍绑定主宠物，新实例使用自身的 Services。</summary>
public interface IPetInstanceService
{
    string ApiVersion { get; }
    /// <summary>以 owner/key 创建独立桌宠；同一标识不可重复。调用方在停用或启动失败时释放实例。</summary>
    IPetInstance Create(string owner, string key, string appearancePath);
    IReadOnlyList<PetInstanceInfo> List();
}

public sealed record PetInstanceInfo(string Owner, string Key, string DisplayName, bool Visible);

/// <summary>一只桌宠的生命周期租约；服务、菜单、动画与活动仅作用于此实例。释放后不得继续使用所取得的服务。</summary>
public interface IPetInstance : IDisposable
{
    string Owner { get; }
    string Key { get; }
    bool IsAlive { get; }
    ServiceRegistry Services { get; }
    /// <summary>恢复实例窗口，不创建第二个副本；保留上次位置。</summary>
    void Show();
    /// <summary>暂时收起实例，保留租约与恢复入口，不退出宿主。</summary>
    void Hide();
}
