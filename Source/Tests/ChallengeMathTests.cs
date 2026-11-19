namespace Expeditions.Tests;

public class ChallengeMathTests
{
    static readonly Curve Ratio = new([(0, 0), (1, 0.6), (2, 0.95)]);

    [Fact]
    public void Curve_interpolates_and_clamps()
    {
        Assert.Equal(0.3, Ratio.Evaluate(0.5), 9);
        Assert.Equal(0.775, Ratio.Evaluate(1.5), 9);
        Assert.Equal(0, Ratio.Evaluate(-1), 9);
        Assert.Equal(0.95, Ratio.Evaluate(3), 9);
    }

    [Fact]
    public void PawnScore_averages_ratios() =>
        Assert.Equal(75, ChallengeMath.PawnScore([new(0.9, 1, 0.9), new(2, 1, 4)]), 9);

    [Fact]
    public void PawnScore_caps_each_term() =>
        Assert.Equal(200, ChallengeMath.PawnScore([new(10, 1, 1)]), 9);

    [Fact]
    public void PawnScore_is_zero_without_terms() =>
        Assert.Equal(0, ChallengeMath.PawnScore([]), 9);

    [Fact]
    public void Combine_rules()
    {
        double[] s = [50, 100, 75];
        Assert.Equal(225, ChallengeMath.Combine(s, CombineRule.Sum));
        Assert.Equal(50, ChallengeMath.Combine(s, CombineRule.Min));
        Assert.Equal(100, ChallengeMath.Combine(s, CombineRule.Max));
        Assert.Equal(75, ChallengeMath.Combine(s, CombineRule.Average));
        Assert.Equal(0, ChallengeMath.Combine([], CombineRule.Min));
    }

    [Fact]
    public void ChallengeChance_uses_ratio()
    {
        Assert.Equal(0.3, ChallengeMath.ChallengeChance(50, 100, Ratio), 9);
        Assert.Equal(1, ChallengeMath.ChallengeChance(50, 0, Ratio), 9);
    }

    [Fact]
    public void SuccessChance_weights_and_clamps()
    {
        Assert.Equal(0.5, ChallengeMath.SuccessChance([(0.6, 2), (0.3, 1)]), 9);
        Assert.Equal(0.95, ChallengeMath.SuccessChance([(1.0, 1)]), 9);
        Assert.Equal(0.05, ChallengeMath.SuccessChance([(0, 1)]), 9);
        Assert.Equal(0.95, ChallengeMath.SuccessChance([]), 9);
    }

    [Fact]
    public void ArmedBonus_spreads()
    {
        Assert.Equal(7.5, ChallengeMath.ArmedBonus(10, 0.5, 1.0, 0.5), 9);
        Assert.Equal(2.5, ChallengeMath.ArmedBonus(10, 0.5, 0.0, 0.5), 9);
    }

    [Fact]
    public void Stealth_capped_when_loud()
    {
        Assert.Equal(90, ChallengeMath.StealthCap(90, Ride.Foot, 30));
        Assert.Equal(30, ChallengeMath.StealthCap(90, Ride.Shuttle, 30));
    }
}
