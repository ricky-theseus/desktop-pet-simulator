using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class PetAnimationTests
{
    private const string Definition = """
        { "formatVersion": 1, "id": "example", "displayName": "示例", "frameSize": 64,
          "frames": ["idle.png", "blink.png", "eat.png"],
          "actions": {
            "idle": { "frames": [0, 1], "fps": 2, "loop": true },
            "eat": { "frames": [2, 2], "fps": 4, "loop": false },
            "held": { "frames": [1], "fps": 2, "loop": true }
          } }
        """;

    [Fact]
    public void IdleLoopsAndEatingReturnsToIdle()
    {
        var player = new PetAnimationTimeline(PetAppearanceDefinition.FromJson(Definition));
        player.Tick(0.5);
        Assert.Equal(1, player.FrameIndex);
        player.Tick(0.5);
        Assert.Equal(0, player.FrameIndex);
        player.Play("eat");
        Assert.Equal(2, player.FrameIndex);
        player.Tick(0.49);
        Assert.Equal("eat", player.Action);
        player.Tick(0.02);
        Assert.Equal("idle", player.Action);
    }

    [Fact]
    public void MissingActionsFallBackToIdleAndHeldInterruptsEating()
    {
        var player = new PetAnimationTimeline(PetAppearanceDefinition.FromJson(Definition));
        player.Play("eat");
        player.Play("held");
        player.Tick(30);
        Assert.Equal("held", player.Action);
        player.Play("missing-mod-action");
        Assert.Equal("idle", player.Action);
    }

    [Theory]
    [InlineData("\"formatVersion\": 1", "\"formatVersion\": 2")]
    [InlineData("\"idle\":", "\"missing\":")]
    [InlineData("[2, 2]", "[2, 99]")]
    [InlineData("\"fps\": 4", "\"fps\": 0")]
    [InlineData("idle.png", "../other.png")]
    [InlineData("idle.png", "https://example.com/image.png")]
    public void InvalidAppearanceFailsBeforePlayback(string source, string replacement)
    {
        Assert.Throws<ArgumentException>(() => PetAppearanceDefinition.FromJson(Definition.Replace(source, replacement)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidTimeDoesNotCorruptPlayback(double delta)
    {
        var player = new PetAnimationTimeline(PetAppearanceDefinition.FromJson(Definition));
        Assert.Throws<ArgumentOutOfRangeException>(() => player.Tick(delta));
        Assert.Equal(0, player.FrameIndex);
    }
}
