namespace DesktopPet.Core;

public sealed record ModHostOptions(string ApiVersion);

public sealed class ModContext(ServiceRegistry services, ICollection<string> lifecycleLog)
{
    public ServiceRegistry Services { get; } = services;

    public ICollection<string> LifecycleLog { get; } = lifecycleLog;
}

public sealed class ModHost : IModRuntimeService
{
    private readonly ModHostOptions options;
    private readonly ServiceRegistry services;
    private readonly List<IMod> mods = [];
    private readonly List<string> lifecycleLog = [];
    private readonly HashSet<string> running = [];
    private readonly HashSet<string> pendingCleanup = [];
    private bool started;

    public ModHost(ModHostOptions options, ServiceRegistry? services = null)
    {
        this.options = options;
        this.services = services ?? new ServiceRegistry();
    }

    public IReadOnlyList<string> LifecycleLog => lifecycleLog;
    public string ApiVersion => "1.0";

    public bool Contains(string id) => mods.Any(mod => mod.Manifest.Id == id);

    public bool IsRunning(string id) => running.Contains(id);

    public void Register(IMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);

        if (mods.Any(existing => existing.Manifest.Id == mod.Manifest.Id))
        {
            throw new InvalidOperationException($"A Mod with id '{mod.Manifest.Id}' is already registered.");
        }

        if (mod.Manifest.ApiVersion != options.ApiVersion)
        {
            throw new InvalidOperationException(
                $"Mod '{mod.Manifest.Id}' targets API {mod.Manifest.ApiVersion}, but the host provides {options.ApiVersion}.");
        }

        if (started)
        {
            throw new InvalidOperationException("Mods cannot be registered after the host has started.");
        }

        mods.Add(mod);
    }

    public void StartAll(Func<string, bool>? shouldStart = null)
    {
        if (started)
        {
            return;
        }

        // 先进入运行阶段；后续启动失败时，已成功启动的 Mod 仍可由 StopAll 释放。
        started = true;
        foreach (var mod in mods)
        {
            if (shouldStart?.Invoke(mod.Manifest.Id) ?? true) SetEnabled(mod.Manifest.Id, true);
        }
    }

    public void SetEnabled(string id, bool enabled)
    {
        var mod = mods.FirstOrDefault(item => item.Manifest.Id == id)
            ?? throw new InvalidOperationException($"找不到已注册的 Mod：{id}");
        if (!started) throw new InvalidOperationException("宿主尚未启动，不能切换 Mod 运行状态。");
        if (running.Contains(id) == enabled && !pendingCleanup.Contains(id)) return;

        var context = new ModContext(services, lifecycleLog);
        if (pendingCleanup.Contains(id))
        {
            // 初始化失败留下的资源必须先清理，避免重启时叠加旧监听或后台任务。
            StopMod(mod, context);
            if (!enabled) return;
        }
        if (enabled)
        {
            try { mod.Start(context); }
            catch (Exception startError)
            {
                try { mod.Stop(context); }
                catch (Exception cleanupError)
                {
                    pendingCleanup.Add(id);
                    throw new AggregateException($"启动 Mod“{mod.Manifest.Name}”({id}) 失败，释放未完成资源时也发生错误。", startError, cleanupError);
                }
                throw new InvalidOperationException($"启动 Mod“{mod.Manifest.Name}”({id}) 失败：{startError.Message}", startError);
            }
            running.Add(id);
            return;
        }

        StopMod(mod, context);
    }

    private void StopMod(IMod mod, ModContext context)
    {
        var id = mod.Manifest.Id;
        try { mod.Stop(context); }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"停止 Mod“{mod.Manifest.Name}”({id}) 失败：{exception.Message}", exception);
        }
        running.Remove(id);
        pendingCleanup.Remove(id);
    }

    public void StopAll()
    {
        if (!started) return;

        var errors = new List<Exception>();
        for (var index = mods.Count - 1; index >= 0; index--)
        {
            try { SetEnabled(mods[index].Manifest.Id, false); }
            catch (Exception exception) { errors.Add(exception); }
        }

        started = running.Count > 0 || pendingCleanup.Count > 0;
        if (errors.Count > 0) throw new AggregateException("部分 Mod 未能停止；其他运行中的 Mod 已完成停止尝试。", errors);
    }
}
