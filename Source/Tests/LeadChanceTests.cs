namespace Expeditions.Tests;

public class LeadChanceTests
{
    static readonly Curve Tech = new([(0, 1), (4, 0.8), (5, 0.45), (6, 0.2), (7, 0.05)]);
    static readonly Curve Value = new([(0, 1), (50, 0.85), (500, 0.55), (2000, 0.3), (10000, 0.1)]);

    [Fact]
    public void Lead_multiplies_and_clamps()
    {
        Assert.Equal(0.68, LeadChance.Of(4, 50, Tech, Value), 9);
        Assert.Equal(0.01, LeadChance.Of(7, 10000, Tech, Value), 9);
        Assert.Equal(0.99, LeadChance.Of(0, 0, Tech, Value), 9);
    }
}
