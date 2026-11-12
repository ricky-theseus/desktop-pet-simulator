using DesktopPet.Core;

namespace DesktopPet.OfficialMushroom;

public sealed class MushroomMod : IMod
{
    public ModManifest Manifest { get; } = new(
        "official-mushroom",
        "官方蘑菇",
        "0.1.0",
        "1.0");

    public PetDefinition PetDefinition { get; } = new("mushroom", "蘑菇");

    public void Start(ModContext context)
    {
    }

    public void Stop(ModContext context)
    {
    }
}
