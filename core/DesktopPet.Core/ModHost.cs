namespace DesktopPet.Core;

public sealed record ModHostOptions(string ApiVersion);

public sealed class ModContext(ServiceRegistry services, ICollection<string> lifecycleLog)
{
    public ServiceRegistry Services { get; } = services;

    public ICollection<string> LifecycleLog { get; } = lifecycleLog;
}

public sealed class ModHost
{
    private readonly ModHostOptions options;
    private readonly ServiceRegistry services;
    private readonly List<IMod> mods = [];
    private readonly List<string> lifecycleLog = [];
    private bool started;

    public ModHost(ModHostOptions options, ServiceRegistry? services = null)
    {
        this.options = options;
        this.services = services ?? new ServiceRegistry();
    }

    public IReadOnlyList<string> LifecycleLog => lifecycleLog;

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

    public void StartAll()
    {
        if (started)
        {
            return;
        }

        var context = new ModContext(services, lifecycleLog);
        foreach (var mod in mods)
        {
            mod.Start(context);
        }

        started = true;
    }

    public void StopAll()
    {
        if (!started)
        {
            return;
        }

        var context = new ModContext(services, lifecycleLog);
        for (var index = mods.Count - 1; index >= 0; index--)
        {
            mods[index].Stop(context);
        }

        started = false;
    }
}
