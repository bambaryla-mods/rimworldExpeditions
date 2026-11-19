namespace Expeditions.Tests;

public class FoodMathTests
{
    [Fact]
    public void Food_required_and_shortfall()
    {
        Assert.Equal(28.0, FoodMath.Required([1.6, 1.6, 0.8], 7), 9);
        Assert.Equal(8, FoodMath.Shortfall(28, 20), 9);
        Assert.Equal(0, FoodMath.Shortfall(28, 30), 9);
    }
}

public class LootFitTests
{
    [Fact]
    public void Loot_keeps_value_per_kg()
    {
        LootStack[] s = [new(10, 1, 10), new(100, 20, 3), new(5, 0, 50)];
        Assert.Equal([10, 0, 50], LootFit.Fit(s, 25));
        Assert.Equal([10, 2, 50], LootFit.Fit(s, 50));
        Assert.Equal([0, 0, 50], LootFit.Fit(s, 0));
    }
}

public class TravelMathTests
{
    [Fact]
    public void Travel_by_ride()
    {
        var t = new TravelTuning();
        Assert.Equal(6.5, TravelMath.TripDays(Ride.Foot, 3, 30, 0.5, t), 9);
        Assert.Equal(4.05, TravelMath.TripDays(Ride.DropPods, 3, 30, 0.5, t), 9);
        Assert.Equal(1.6, TravelMath.TripDays(Ride.Shuttle, 3, 30, 0.5, t), 9);
        Assert.Equal(3.5, TravelMath.TripDays(Ride.Vehicles, 3, 30, 0.5, t), 9);
        Assert.False(TravelMath.IsLoud(Ride.Foot));
        Assert.True(TravelMath.IsLoud(Ride.DropPods));
    }
}

public class ProjectionTests
{
    [Fact]
    public void Projection_axes()
    {
        Vec3 c = new(1, 0, 0), n = new(0, 1, 0);
        double a = 0.1;
        Assert.Equal((0.0, 0.0), Projection.AzimuthalEquidistant(c, c, n));
        var up = Projection.AzimuthalEquidistant(c, new(Math.Cos(a), Math.Sin(a), 0), n);
        Assert.Equal(0, up.x, 9);
        Assert.Equal(a, up.y, 9);
        var side = Projection.AzimuthalEquidistant(c, new(Math.Cos(a), 0, -Math.Sin(a)), n);
        Assert.Equal(a, side.x, 9);
        Assert.Equal(0, side.y, 9);
    }
}
