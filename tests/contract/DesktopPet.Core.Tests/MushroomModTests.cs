using DesktopPet.Core;
using DesktopPet.OfficialMushroom;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class MushroomModTests
{
    [Fact]
    public void ProvidesMushroomPetDefinition()
    {
        var mushroom = new MushroomMod();

        Assert.Equal("official-mushroom", mushroom.Manifest.Id);
        Assert.Equal("蘑菇", mushroom.PetDefinition.DisplayName);
        Assert.Equal("mushroom", mushroom.PetDefinition.AssetKey);
    }
}
