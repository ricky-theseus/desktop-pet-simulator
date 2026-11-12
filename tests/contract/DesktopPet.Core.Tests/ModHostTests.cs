using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class ModHostTests
{
    [Fact]
    public void StartsAndStopsModsInRegistrationOrder()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var first = new RecordingMod("first");
        var second = new RecordingMod("second");

        host.Register(first);
        host.Register(second);

        host.StartAll();
        host.StopAll();

        Assert.Equal(["start:first", "start:second", "stop:second", "stop:first"], host.LifecycleLog);
    }

    [Fact]
    public void RejectsDuplicateModIds()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        host.Register(new RecordingMod("same"));

        var error = Assert.Throws<InvalidOperationException>(() => host.Register(new RecordingMod("same")));

        Assert.Contains("same", error.Message);
    }

    [Fact]
    public void ExposesRegisteredServicesToMods()
    {
        var services = new ServiceRegistry();
        var provider = new FakeDesktopProvider();
        services.Add<IDesktopProvider>(provider);
        var host = new ModHost(new ModHostOptions("1.0"), services);
        var mod = new ServiceReadingMod();

        host.Register(mod);
        host.StartAll();

        Assert.Same(provider, mod.Provider);
    }

    private sealed class RecordingMod(string id) : IMod
    {
        public ModManifest Manifest { get; } = new(id, id, "1.0", "1.0");

        public void Start(ModContext context) => context.LifecycleLog.Add($"start:{Manifest.Id}");

        public void Stop(ModContext context) => context.LifecycleLog.Add($"stop:{Manifest.Id}");
    }

    private sealed class ServiceReadingMod : IMod
    {
        public ModManifest Manifest { get; } = new("service-reader", "Service Reader", "1.0", "1.0");

        public IDesktopProvider? Provider { get; private set; }

        public void Start(ModContext context) => Provider = context.Services.GetRequired<IDesktopProvider>();

        public void Stop(ModContext context)
        {
        }
    }

    private sealed class FakeDesktopProvider : IDesktopProvider
    {
        public string PlatformId => "test";
    }
}
