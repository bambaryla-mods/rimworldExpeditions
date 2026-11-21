// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// Vanilla quest sites on the board: how hard the fight is, and how big the raid that answers a partial win.
public static class SiteLinkMath
{
    public static double FightRequirement(double threatPoints, double perPoint, double min) => Math.Max(min, threatPoints * perPoint);

    public static double RetaliationPoints(double threatNow, double factor, double floor) => Math.Max(floor, threatNow * factor);
}
