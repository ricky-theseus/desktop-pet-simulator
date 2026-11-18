using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class PetDisplayTests
{
    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(.49, 1)]
    [InlineData(2.01, 1)]
    [InlineData(1, .49)]
    [InlineData(1, 1.01)]
    [InlineData(1, double.NaN)]
    public void RejectsInvisibleOrUnboundedDisplay(double scale, double opacity)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PetDisplayOptions(scale, opacity).Validate());

    [Fact]
    public void ScopesPreserveLegacyRegistrationAndRejectUnknownValues()
    {
        var menu = new PetMenuService();
        using var local = menu.Register("sample", "local", "动作", () => { });
        using var shared = menu.Register("sample", "shared", "农场", () => { }, PetMenuScope.Shared);
        using var tray = menu.Register("sample", "tray", "恢复", () => { }, PetMenuScope.TrayOnly);
        Assert.Equal(new[] { PetMenuScope.Local, PetMenuScope.Shared, PetMenuScope.TrayOnly }, menu.Items.Select(item => item.Scope));
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.Register("sample", "bad", "错误", () => { }, (PetMenuScope)42));
        Assert.Equal(3, menu.Items.Count);
    }
}
