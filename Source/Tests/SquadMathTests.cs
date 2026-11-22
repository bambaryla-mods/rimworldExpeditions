namespace Expeditions.Tests;

public class SquadMathTests
{
    [Fact]
    public void Contract_fits_with_margin()
    {
        Assert.True(SquadMath.ContractFits(6 * 60000, 5, 1));
        Assert.False(SquadMath.ContractFits(6 * 60000 - 1, 5, 1));
    }

    [Fact]
    public void Ratio_handles_zero_sides()
    {
        Assert.Equal(double.PositiveInfinity, SquadMath.FightRatio(100, 0));
        Assert.Equal(0, SquadMath.FightRatio(0, 100));
        Assert.Equal(2, SquadMath.FightRatio(200, 100));
    }

    [Fact]
    public void Infinite_ratio_always_wins()
    {
        var c = new Curve(new[] { (0.5, 0.15), (1.0, 0.5), (3.0, 0.97) });
        Assert.True(SquadMath.SquadWins(double.PositiveInfinity, 0.999, c));
        Assert.True(SquadMath.SquadWins(1, 0.49, c));
        Assert.False(SquadMath.SquadWins(1, 0.51, c));
    }
}
