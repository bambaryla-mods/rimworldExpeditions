namespace Expeditions.Tests;

public class ProspectSeedTests
{
    [Fact]
    public void Seed_windows()
    {
        Assert.Equal(0, ProspectSeed.WindowAt(0));
        Assert.Equal(0, ProspectSeed.WindowAt(149999));
        Assert.Equal(1, ProspectSeed.WindowAt(150000));
        Assert.Equal(50000, ProspectSeed.TicksUntilNextWindow(100000));
    }

    [Fact]
    public void Seed_is_deterministic_and_fair()
    {
        Assert.Equal(ProspectSeed.Roll01(7, 3, "Steel"), ProspectSeed.Roll01(7, 3, "Steel"));
        Assert.True(ProspectSeed.HasLead(7, 3, "Steel", 1.0));
        Assert.False(ProspectSeed.HasLead(7, 3, "Steel", 0.0));
        int hits = Enumerable.Range(0, 2000).Count(i => ProspectSeed.HasLead(7, 3, "Item" + i, 0.3));
        Assert.InRange(hits, 500, 700);
        Assert.Contains(Enumerable.Range(0, 50), i => ProspectSeed.HasLead(7, 3, "K" + i, 0.5) != ProspectSeed.HasLead(7, 4, "K" + i, 0.5));
    }
}
