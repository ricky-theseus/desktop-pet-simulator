using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class PetExtensionTests
{
    [Fact]
    public void MenuCommandsRespectEnablementAndReleaseTheirCallback()
    {
        var menu = new PetMenuService();
        var calls = 0;
        using var command = menu.Register("sample", "open", "开始", () => calls++);
        var id = Assert.Single(menu.Items).Id;
        Assert.True(menu.Invoke(id));
        command.Enabled = false;
        Assert.True(menu.Invoke(id));
        Assert.Equal(1, calls);
        command.Text = "结束";
        Assert.Equal("结束", Assert.Single(menu.Items).Text);
        command.Dispose();
        command.Dispose();
        Assert.Empty(menu.Items);
        Assert.False(menu.Invoke(id));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void MenuKeysAreUniquePerOwnerAndReusableAfterRelease()
    {
        var menu = new PetMenuService();
        var first = menu.Register("one", "open", "打开", () => { });
        Assert.Throws<InvalidOperationException>(() => menu.Register("one", "open", "打开", () => { }));
        using var other = menu.Register("two", "open", "打开", () => { });
        first.Dispose();
        using var replacement = menu.Register("one", "open", "重新打开", () => { });
        first.Dispose();
        Assert.Equal(2, menu.Items.Count);
    }

    [Fact]
    public void ExclusiveActivityCannotBeStolenOrReleasedByAnOldLease()
    {
        var service = new PetActivityService();
        Assert.False(service.IsBusy);
        var first = service.TryAcquire("one")!;
        Assert.True(service.IsBusy);
        Assert.Null(service.TryAcquire("two"));
        first.Dispose();
        using var second = service.TryAcquire("two");
        Assert.NotNull(second);
        first.Dispose();
        Assert.True(service.IsBusy);
        second!.Dispose();
        Assert.False(service.IsBusy);
    }
}
