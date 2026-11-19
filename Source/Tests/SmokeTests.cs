namespace Expeditions.Tests;

public class SmokeTests
{
    [Fact]
    public void There_are_four_rides() => Assert.Equal(4, Enum.GetValues<Ride>().Length);
}
