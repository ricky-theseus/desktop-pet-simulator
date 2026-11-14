using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class OfflineSteamServiceTests
{
    [Fact]
    public void PublicBuildCanReadPlatformStatusWithoutNativeSdk()
    {
        ISteamService service = new OfflineSteamService();
        Assert.Equal("1.0", service.ApiVersion);
        Assert.False(service.Status.Available);
        Assert.Equal(0u, service.Status.AppId);
        Assert.Empty(service.GetSubscriptions());
    }
}
