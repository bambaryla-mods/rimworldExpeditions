namespace Expeditions.Tests;

public class WeightedOrderTests
{
    [Fact]
    public void Leaves_out_entries_without_weight()
    {
        var rng = new Random(1);
        List<int> order = WeightedOrder.Order([1, 0, 2, -1], rng.NextDouble);
        Assert.Equal([0, 2], order.OrderBy(i => i));
    }

    [Fact]
    public void Heavier_entries_come_first_in_proportion()
    {
        var rng = new Random(7);
        int heavyFirst = 0;
        const int trials = 20000;
        for (int i = 0; i < trials; i++)
            if (WeightedOrder.Order([1, 0.2], rng.NextDouble)[0] == 0) heavyFirst++;
        // P(first) = 1 / (1 + 0.2) = 0.833
        Assert.InRange(heavyFirst / (double)trials, 0.81, 0.86);
    }

    [Fact]
    public void Equal_weights_keep_every_entry()
    {
        var rng = new Random(3);
        Assert.Equal([0, 1, 2, 3, 4], WeightedOrder.Order([1, 1, 1, 1, 1], rng.NextDouble).OrderBy(i => i));
    }
}
