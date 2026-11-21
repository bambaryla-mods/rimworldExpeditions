namespace Expeditions.Tests;

public class WorksiteMathTests
{
    [Fact]
    public void Share_by_outcome()
    {
        Assert.Equal(1200, WorksiteMath.Share(2000, 0.6, Outcome.Success));
        Assert.Equal(600, WorksiteMath.Share(2000, 0.6, Outcome.Partial));
        Assert.Equal(0, WorksiteMath.Share(2000, 0.6, Outcome.Failure));
        Assert.Equal(1, WorksiteMath.Share(2, 0.6, Outcome.Success));   // floor(1.2)
        Assert.Equal(1, WorksiteMath.Share(2, 0.6, Outcome.Partial));   // half of 1, at least 1
        Assert.Equal(0, WorksiteMath.Share(1, 0.6, Outcome.Success));   // floor(0.6): nothing to take
        Assert.Equal(0, WorksiteMath.Share(1, 0.6, Outcome.Partial));
        Assert.Equal(0, WorksiteMath.Share(2000, 0.6, Outcome.Gone));
    }

    [Fact]
    public void Fight_scales_with_guards()
    {
        Assert.Equal(450, WorksiteMath.FightRequirement(5, 90), 9);
        Assert.Equal(100, WorksiteMath.FightRequirement(1, 90), 9);
        Assert.Equal(100, WorksiteMath.FightRequirement(0, 90), 9);
    }
}
