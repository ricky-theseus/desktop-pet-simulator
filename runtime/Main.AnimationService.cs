using DesktopPet.Core;

namespace DesktopPet.Runtime;

public partial class Main : IPetAnimationService
{
    public string? AppearanceId => pet?.AppearanceId;

    public IPetAnimationLayer CreateLayer(string owner, string definitionPath)
    {
        if (pet is null) throw new InvalidOperationException("Pet view is not ready.");
        return pet.CreateAnimationLayer(owner, definitionPath, () => CanPose);
    }
}
