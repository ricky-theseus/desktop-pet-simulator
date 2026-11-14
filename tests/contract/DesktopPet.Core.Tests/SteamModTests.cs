using DesktopPet.Core;
using DesktopPet.OfficialSteam;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class SteamModTests
{
    [Fact]
    public void UnconfiguredAppStaysOfflineAndLifecycleIsSafeWithoutNativeSdk()
    {
        var service = new SteamMod(0);
        var registry = new ServiceRegistry();
        var context = new ModContext(registry, new List<string>());
        service.Start(context);
        service.Start(context);
        service.Tick();
        Assert.Same(service, registry.GetRequired<ISteamService>());
        Assert.False(service.Status.Available);
        Assert.Empty(service.GetSubscriptions());
        service.Stop(context);
        service.Stop(context);
        Assert.False(service.Status.Available);
    }
}
