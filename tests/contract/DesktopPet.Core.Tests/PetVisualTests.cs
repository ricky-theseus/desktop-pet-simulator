using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class PetVisualTests
{
    [Fact]
    public void NeutralIsValidAndUnsafeTransformsAreRejected()
    {
        Assert.True(PetVisualPose.Neutral.IsValid);
        Assert.False((PetVisualPose.Neutral with { ScaleX = float.NaN }).IsValid);
        Assert.False((PetVisualPose.Neutral with { X = float.PositiveInfinity }).IsValid);
        Assert.False((PetVisualPose.Neutral with { ScaleY = -1 }).IsValid);
        Assert.False((PetVisualPose.Neutral with { Rotation = 3 }).IsValid);
        Assert.False((PetVisualPose.Neutral with { Skew = float.NaN }).IsValid);
    }
}
