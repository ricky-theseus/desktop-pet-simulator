using DesktopPet.Core;
using Steamworks;

namespace DesktopPet.OfficialSteam;

/// <summary>只读 Steam 接入；无 App ID 时不触碰原生 SDK，保持离线可用。</summary>
public sealed class SteamMod(uint appId) : IMod, ISteamService
{
    private bool initialized;
    public ModManifest Manifest { get; } = new("official-steam", "Steam 连接", "0.1.0", "1.0");
    public string ApiVersion => "1.0";
    public SteamStatus Status { get; private set; } = new(false, appId, appId == 0 ? "尚未配置 Steam App ID，本地 Mod 功能可正常使用。" : "Steam 尚未启动。");

    public void Start(ModContext context)
    {
        context.Services.Add<ISteamService>(this);
        if (appId == 0 || initialized) return;
        try
        {
            // App ID 由 Steam 启动环境或开发者本地 steam_appid.txt 提供，不自动借用其他游戏的 ID。
            if (!SteamAPI.Init())
            {
                Status = new(false, appId, "Steam 初始化失败，请检查客户端、App ID 与当前账号的应用权限。");
                return;
            }
            initialized = true;
            if (SteamUtils.GetAppID().m_AppId != appId)
            {
                SteamAPI.Shutdown();
                initialized = false;
                Status = new(false, appId, "Steam 环境中的 App ID 与项目配置不一致。");
                return;
            }
            RefreshStatus();
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or TypeInitializationException)
        {
            if (initialized)
            {
                try { SteamAPI.Shutdown(); }
                catch (Exception shutdownError) when (shutdownError is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { }
            }
            initialized = false;
            Status = new(false, appId, "Steam 原生运行库缺失或版本不匹配；本地 Mod 仍可使用。");
        }
    }

    public void Tick()
    {
        if (!initialized) return;
        SteamAPI.RunCallbacks();
    }

    private void RefreshStatus()
    {
        Status = SteamUser.BLoggedOn()
            ? new(true, appId, "Steam 已连接", SteamFriends.GetPersonaName())
            : new(false, appId, "Steam 当前离线，本地内容仍可使用。");
    }

    public IReadOnlyList<WorkshopSubscription> GetSubscriptions()
    {
        if (!initialized) return [];
        RefreshStatus();
        if (!Status.Available) return [];
        var count = SteamUGC.GetNumSubscribedItems();
        if (count > 4096) throw new InvalidOperationException("订阅数量超过当前管理器上限 4096。");
        var ids = new PublishedFileId_t[count];
        var actual = SteamUGC.GetSubscribedItems(ids, count);
        var result = new List<WorkshopSubscription>();
        foreach (var id in ids.Take((int)Math.Min(actual, count)))
        {
            var state = (EItemState)SteamUGC.GetItemState(id);
            var installed = state.HasFlag(EItemState.k_EItemStateInstalled);
            string? directory = null;
            if (installed && SteamUGC.GetItemInstallInfo(id, out _, out var folder, 4096, out _)) directory = folder;
            result.Add(new(id.m_PublishedFileId, directory is not null,
                state.HasFlag(EItemState.k_EItemStateNeedsUpdate) || state.HasFlag(EItemState.k_EItemStateDownloading), directory));
        }
        return result;
    }

    public void Stop(ModContext context)
    {
        if (initialized) SteamAPI.Shutdown();
        initialized = false;
        Status = new(false, appId, "Steam 连接已关闭。");
    }
}
