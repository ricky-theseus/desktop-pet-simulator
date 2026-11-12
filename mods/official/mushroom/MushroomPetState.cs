namespace DesktopPet.OfficialMushroom;

public sealed class MushroomPetState
{
    private const float BounceDecayPerSecond = 1f;

    public float IdleTime { get; private set; }

    public int Affection { get; private set; }

    public float BounceEnergy { get; private set; }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta time cannot be negative.");
        }

        IdleTime += deltaSeconds;
        BounceEnergy = MathF.Max(0f, BounceEnergy - deltaSeconds * BounceDecayPerSecond);
    }

    public void Interact()
    {
        Affection++;
        BounceEnergy = 1f;
    }
}
