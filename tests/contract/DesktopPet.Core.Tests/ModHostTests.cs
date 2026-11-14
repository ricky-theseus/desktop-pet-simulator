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

    [Fact]
    public void PredicateAndRepeatedTogglesDoNotDuplicateLifecycleCalls()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var mod = new ResourceMod("optional");
        host.Register(mod);
        host.StartAll(_ => false);
        IModRuntimeService runtime = host;

        Assert.Equal("1.0", runtime.ApiVersion);
        Assert.True(runtime.Contains("optional"));
        Assert.False(runtime.Contains("unknown"));
        Assert.False(runtime.IsRunning("unknown"));
        Assert.False(mod.Active);
        runtime.SetEnabled("optional", true);
        runtime.SetEnabled("optional", true);
        host.StartAll();
        Assert.True(runtime.IsRunning("optional"));
        Assert.Equal(1, mod.Starts);
        runtime.SetEnabled("optional", false);
        runtime.SetEnabled("optional", false);
        Assert.False(runtime.IsRunning("optional"));
        Assert.False(mod.Active);
        Assert.Equal(1, mod.Stops);

        runtime.SetEnabled("optional", true);
        Assert.True(mod.Active);
        Assert.Equal(2, mod.Starts);
        host.StopAll();
        host.StopAll();
        Assert.Equal(2, mod.Stops);
        host.StartAll();
        Assert.True(mod.Active);
        Assert.Equal(3, mod.Starts);
        host.StopAll();
    }

    [Fact]
    public void FailedStartReleasesPartialResourcesAndKeepsEarlierModsStoppable()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var first = new ResourceMod("first");
        var failing = new ResourceMod("failing") { FailStart = true };
        host.Register(first);
        host.Register(failing);

        var error = Assert.Throws<InvalidOperationException>(() => host.StartAll());

        Assert.Contains("failing", error.Message);
        Assert.False(failing.Active);
        Assert.Equal(1, failing.Stops);
        Assert.False(host.IsRunning("failing"));
        Assert.True(host.IsRunning("first"));
        host.StopAll();
        Assert.False(first.Active);
        Assert.Equal(1, first.Stops);
    }

    [Fact]
    public void FailedHotStartCanBeRetriedAfterCleanup()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var mod = new ResourceMod("optional") { FailStart = true };
        host.Register(mod);
        host.StartAll(_ => false);

        Assert.Throws<InvalidOperationException>(() => host.SetEnabled("optional", true));
        Assert.False(mod.Active);
        Assert.False(host.IsRunning("optional"));
        mod.FailStart = false;
        host.SetEnabled("optional", true);
        Assert.True(mod.Active);
        Assert.Equal(2, mod.Starts);
        host.StopAll();
    }

    [Fact]
    public void StopAllAttemptsEveryRunningModAndAllowsFailedStopsToBeRetried()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var first = new ResourceMod("first") { FailStop = true };
        var second = new ResourceMod("second") { FailStop = true };
        var third = new ResourceMod("third");
        host.Register(first);
        host.Register(second);
        host.Register(third);
        host.StartAll();

        var error = Assert.Throws<AggregateException>(() => host.StopAll());

        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal(["start:first", "start:second", "start:third", "stop:third", "stop:second", "stop:first"], host.LifecycleLog);
        Assert.True(host.IsRunning("first"));
        Assert.True(host.IsRunning("second"));
        Assert.False(host.IsRunning("third"));
        first.FailStop = false;
        second.FailStop = false;
        host.StopAll();
        Assert.False(first.Active);
        Assert.False(second.Active);
        Assert.Equal(1, third.Stops);
    }

    [Fact]
    public void FailedStartAndFailedCleanupAreBothReported()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var mod = new ResourceMod("failing") { FailStart = true, FailStop = true };
        host.Register(mod);

        var error = Assert.Throws<AggregateException>(() => host.StartAll());

        Assert.Contains(error.InnerExceptions, item => item.Message == "start failure");
        Assert.Contains(error.InnerExceptions, item => item.Message == "stop failure");
        Assert.Contains("failing", error.Message);
        Assert.False(host.IsRunning("failing"));
        Assert.Throws<AggregateException>(() => host.StopAll());
        Assert.Equal(2, mod.Stops);
        mod.FailStop = false;
        host.StopAll();
        Assert.False(mod.Active);
        Assert.Equal(3, mod.Stops);
    }

    [Fact]
    public void RetryAfterFailedCleanupStopsOldResourcesBeforeStartingAgain()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var mod = new ResourceMod("failing") { FailStart = true, FailStop = true };
        host.Register(mod);
        Assert.Throws<AggregateException>(() => host.StartAll());
        mod.FailStart = false;
        mod.FailStop = false;

        host.SetEnabled("failing", true);

        Assert.Equal(["start:failing", "stop:failing", "stop:failing", "start:failing"], host.LifecycleLog);
        Assert.True(host.IsRunning("failing"));
        host.StopAll();
        Assert.False(mod.Active);
    }

    [Fact]
    public void RuntimeRejectsUnknownModsAndCallsBeforeStartup()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        host.Register(new ResourceMod("optional"));
        Assert.Throws<InvalidOperationException>(() => host.SetEnabled("optional", true));
        host.StartAll(_ => false);
        Assert.Contains("missing", Assert.Throws<InvalidOperationException>(() => host.SetEnabled("missing", true)).Message);
        host.StopAll();
    }

    private sealed class ResourceMod(string id) : IMod
    {
        public ModManifest Manifest { get; } = new(id, id, "1.0", "1.0");
        public bool Active { get; private set; }
        public bool FailStart { get; set; }
        public bool FailStop { get; set; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }

        public void Start(ModContext context)
        {
            context.LifecycleLog.Add($"start:{Manifest.Id}");
            Starts++;
            Active = true;
            if (FailStart) throw new InvalidOperationException("start failure");
        }

        public void Stop(ModContext context)
        {
            context.LifecycleLog.Add($"stop:{Manifest.Id}");
            Stops++;
            if (FailStop) throw new InvalidOperationException("stop failure");
            Active = false;
        }
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
