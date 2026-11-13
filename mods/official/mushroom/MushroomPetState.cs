namespace DesktopPet.OfficialMushroom;

public sealed class MushroomPetState
{
    private const float BounceDecayPerSecond = 1f;
    private const float FullnessDecayPerSecond = 0.5f;

    public float IdleTime { get; private set; }

    public int Affection { get; private set; }

    public float BounceEnergy { get; private set; }

    public float Fullness { get; private set; } = 100f;

    public string Mood => Fullness <= 25f ? "hungry" : BounceEnergy >= 0.5f ? "happy" : "calm";

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta time cannot be negative.");
        }

        IdleTime += deltaSeconds;
        BounceEnergy = MathF.Max(0f, BounceEnergy - deltaSeconds * BounceDecayPerSecond);
        Fullness = MathF.Max(0f, Fullness - deltaSeconds * FullnessDecayPerSecond);
    }

    public void Interact()
    {
        Affection++;
        BounceEnergy = 1f;
    }

    public void Feed()
    {
        Fullness = MathF.Min(100f, Fullness + 20f);
        Interact();
    }
}
