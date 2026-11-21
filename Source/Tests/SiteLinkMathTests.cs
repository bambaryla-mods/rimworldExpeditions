namespace Expeditions.Tests;

public class SiteLinkMathTests
{
    [Fact]
    public void Fight_follows_threat_points()
    {
        Assert.Equal(450, SiteLinkMath.FightRequirement(450, 1, 100), 9);
        Assert.Equal(225, SiteLinkMath.FightRequirement(450, 0.5, 100), 9);
        Assert.Equal(100, SiteLinkMath.FightRequirement(40, 1, 100), 9);
        Assert.Equal(100, SiteLinkMath.FightRequirement(0, 1, 100), 9);
    }

    [Fact]
    public void A_site_without_threat_points_uses_the_fallback()
    {
        Assert.Equal(450, SiteLinkMath.EffectiveThreat(450, 800), 9);
        Assert.Equal(800, SiteLinkMath.EffectiveThreat(0, 800), 9);   // ancient structures, mercenaries
        Assert.Equal(800, SiteLinkMath.EffectiveThreat(-5, 800), 9);
    }

    [Fact]
    public void Retaliation_is_half_the_raid_points_with_a_floor()
    {
        Assert.Equal(1000, SiteLinkMath.RetaliationPoints(2000, 0.5, 300), 9);
        Assert.Equal(300, SiteLinkMath.RetaliationPoints(400, 0.5, 300), 9);
    }
}
