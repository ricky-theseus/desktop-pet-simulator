using DesktopPet.Core;
using Fixture.Dependency;
namespace Fixture;
public sealed class Plugin : IMod
{
    private ModContext? activeContext;
    private IDisposable? subscription;
    public ModManifest Manifest => new("fixture-code", "fixture-code", "1.0", "1.0");
    public void Start(ModContext context)
    {
        activeContext = context;
        var environment = context.Services.GetRequired<IModEnvironment>();
        var provider = context.Services.GetRequired<IDesktopProvider>();
        File.AppendAllText(Path.Combine(environment.DataDirectory, "events.txt"), Helper.Message + ":" + provider.PlatformId + ":" + environment.ApiVersion + "\n");
        File.WriteAllText(Path.Combine(environment.DataDirectory, "active-resource"), "active");
        subscription = context.Services.GetRequired<IModUpdateService>().Subscribe(delta =>
            File.AppendAllText(Path.Combine(environment.DataDirectory, "events.txt"), "tick:" + delta + "\n"));
        if (File.Exists(Path.Combine(environment.ModDirectory, "fail-start"))) throw new InvalidOperationException("fixture start failed");
    }
    public void Stop(ModContext context)
    {
        if (!ReferenceEquals(activeContext, context)) throw new InvalidOperationException("context changed");
        subscription?.Dispose();
        var environment = context.Services.GetRequired<IModEnvironment>();
        File.AppendAllText(Path.Combine(environment.DataDirectory, "events.txt"), "stopped\n");
        if (File.Exists(Path.Combine(environment.ModDirectory, "fail-stop"))) throw new InvalidOperationException("fixture stop failed");
        File.Delete(Path.Combine(environment.DataDirectory, "active-resource"));
    }
}

public sealed class ThrowingConstructorPlugin : IMod
{
    public ThrowingConstructorPlugin() => throw new InvalidOperationException("fixture constructor failed");
    public ModManifest Manifest => new("fixture-code", "fixture-code", "1.0", "1.0");
    public void Start(ModContext context) => throw new InvalidOperationException("unreachable");
    public void Stop(ModContext context) => throw new InvalidOperationException("unreachable");
}
