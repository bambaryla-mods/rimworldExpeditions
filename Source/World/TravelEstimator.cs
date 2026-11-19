using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Walking times from vanilla world pathing, as a caravan of these pawns would take.
public static class TravelEstimator
{
    private static readonly Dictionary<(PlanetTile, PlanetTile), int> nominalCache = new();

    public static void ClearCache() => nominalCache.Clear();

    // Ticks to walk from one tile to another, or -1 when there is no path.
    public static int FootTicks(PlanetTile from, PlanetTile to, List<Pawn> pawns, float massUsage, float massCapacity)
    {
        int ticksPerMove = CaravanTicksPerMoveUtility.GetTicksPerMove(pawns, massUsage, massCapacity, false, null);
        return Ticks(from, to, ticksPerMove);
    }

    // Days at the default caravan speed, for list rows and the map.
    public static float NominalFootDays(PlanetTile from, PlanetTile to)
    {
        if (!nominalCache.TryGetValue((from, to), out int ticks))
            nominalCache[(from, to)] = ticks = Ticks(from, to, CaravanTicksPerMoveUtility.DefaultTicksPerMove);
        return ticks < 0 ? -1f : ticks / (float)GenDate.TicksPerDay;
    }

    private static int Ticks(PlanetTile from, PlanetTile to, int ticksPerMove)
    {
        if (from == to) return 0;
        using WorldPath path = from.Layer.Pather.FindPath(from, to, null);
        if (path == null || !path.Found) return -1;
        return CaravanArrivalTimeEstimator.EstimatedTicksToArrive(from, to, path, 0f, ticksPerMove, Find.TickManager.TicksAbs);
    }
}
