namespace Expeditions.Tests;

public class HookMathTests
{
    [Fact]
    public void Payment_is_split_evenly_and_the_rest_is_silver()
    {
        var (counts, silver) = HookMath.SplitPayment(300, new double[] { 10, 20 });
        Assert.Equal(new[] { 15, 7 }, counts);
        Assert.Equal(10, silver);
    }

    [Fact]
    public void Payment_too_small_for_one_unit_stays_silver()
    {
        var (counts, silver) = HookMath.SplitPayment(5, new double[] { 10 });
        Assert.Equal(new[] { 0 }, counts);
        Assert.Equal(5, silver);
    }

    [Fact]
    public void Payment_with_no_goods_is_all_silver()
    {
        var (counts, silver) = HookMath.SplitPayment(120, new double[0]);
        Assert.Empty(counts);
        Assert.Equal(120, silver);
    }

    [Fact]
    public void Joiners_roll_on_partial_only_with_a_factor()
    {
        Assert.Equal(0.6, HookMath.JoinerChance(0.6, Outcome.Success, 0.5), 9);
        Assert.Equal(0.3, HookMath.JoinerChance(0.6, Outcome.Partial, 0.5), 9);
        Assert.Equal(0, HookMath.JoinerChance(0.6, Outcome.Partial, 0), 9);
        Assert.Equal(0, HookMath.JoinerChance(0.6, Outcome.Failure, 0.5), 9);
    }

    [Fact]
    public void Half_rounds_up()
    {
        Assert.Equal(3, HookMath.HalfUp(5));
        Assert.Equal(2, HookMath.HalfUp(4));
        Assert.Equal(0, HookMath.HalfUp(0));
    }

    [Fact]
    public void Kills_are_capped_below_a_fully_crystallized_brain()
    {
        // 100 kills to the pod threshold of 0.5: 200 kills is a full brain, so 199 is the most allowed.
        Assert.Equal(10, HookMath.CappedKills(0, 10, 100, 0.5));
        Assert.Equal(9, HookMath.CappedKills(190, 50, 100, 0.5));
        Assert.Equal(0, HookMath.CappedKills(199, 5, 100, 0.5));
        Assert.Equal(0, HookMath.CappedKills(250, 5, 100, 0.5));
    }
}
