using DesktopPet.OfficialMushroom;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class MushroomPetStateTests
{
    [Fact]
    public void InteractionIncreasesAffectionAndStartsBounce()
    {
        var state = new MushroomPetState();

        state.Interact();

        Assert.Equal(1, state.Affection);
        Assert.Equal(1f, state.BounceEnergy);
    }

    [Fact]
    public void TickAdvancesIdleTimeAndDecaysBounceEnergy()
    {
        var state = new MushroomPetState();
        state.Interact();

        state.Tick(0.25f);

        Assert.Equal(0.25f, state.IdleTime, precision: 3);
        Assert.Equal(0.75f, state.BounceEnergy, precision: 3);
    }

    [Fact]
    public void FeedingRestoresFullnessAndStartsHappyFeedback()
    {
        var state = new MushroomPetState();
        state.Tick(50f);

        state.Feed();

        Assert.Equal(95f, state.Fullness, precision: 3);
        Assert.Equal(1, state.Affection);
        Assert.Equal("happy", state.Mood);
    }

    [Fact]
    public void FullnessDecaysOverTimeButNeverBecomesNegative()
    {
        var state = new MushroomPetState();

        state.Tick(1000f);

        Assert.Equal(0f, state.Fullness, precision: 3);
        Assert.Equal("hungry", state.Mood);
    }
}
