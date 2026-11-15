using DesktopPet.Core;

namespace DesktopPet.Runtime;

public partial class Main : IPetAnimationService
{
    public string? AppearanceId => pet?.AppearanceId;
    public bool HasActiveLayer => pet?.HasActiveAnimationLayer == true;

    public IPetAnimationLayer CreateLayer(string owner, string definitionPath)
        => CreateLayer(owner, definitionPath, 0);

    public IPetAnimationLayer CreateLayer(string owner, string definitionPath, int priority)
    {
        if (pet is null) throw new InvalidOperationException("Pet view is not ready.");
        return pet.CreateAnimationLayer(owner, definitionPath, priority, () => CanPose);
    }
}
