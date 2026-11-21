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
    public void Turned_back_squad_eats_only_the_days_it_was_out()
    {
        Assert.Equal(2, WorksiteMath.TurnBackFood(10, 2, 10), 9);   // 1 day out and 1 back, of a 10-day trip
        Assert.Equal(0, WorksiteMath.TurnBackFood(10, 0, 10), 9);
        Assert.Equal(10, WorksiteMath.TurnBackFood(10, 12, 10), 9); // never more than was packed
        Assert.Equal(10, WorksiteMath.TurnBackFood(10, 2, 0), 9);   // no trip length known: keep it as it was
    }

    [Fact]
    public void Fight_scales_with_guards()
    {
        Assert.Equal(450, WorksiteMath.FightRequirement(5, 90), 9);
        Assert.Equal(100, WorksiteMath.FightRequirement(1, 90), 9);
        Assert.Equal(100, WorksiteMath.FightRequirement(0, 90), 9);
    }
}
