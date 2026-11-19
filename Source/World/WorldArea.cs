using RimWorld.Planet;
using Verse;

namespace Expeditions;

// The land tiles an expedition can reach from a home tile, by neighbour steps on the surface layer.
public static class WorldArea
{
    private static readonly Dictionary<(PlanetTile home, int range), Dictionary<PlanetTile, int>> cache = new();
    private static readonly List<PlanetTile> neighbors = new();

    public static void ClearCache() => cache.Clear();

    public static List<PlanetTile> TilesInRange(PlanetTile home, int range) =>
        Distances(home, range).Where(kv => !Find.WorldGrid[kv.Key].WaterCovered).Select(kv => kv.Key).ToList();

    // Steps from home for every tile within range, water included so islands and coasts across a bay are reachable.
    public static Dictionary<PlanetTile, int> Distances(PlanetTile home, int range)
    {
        if (cache.TryGetValue((home, range), out var known)) return known;
        var dist = new Dictionary<PlanetTile, int> { [home] = 0 };
        var queue = new Queue<PlanetTile>();
        queue.Enqueue(home);
        while (queue.Count > 0)
        {
            PlanetTile t = queue.Dequeue();
            int d = dist[t];
            if (d >= range) continue;
            Find.WorldGrid.GetTileNeighbors(t, neighbors);
            foreach (PlanetTile n in neighbors)
            {
                if (dist.ContainsKey(n)) continue;
                dist[n] = d + 1;
                queue.Enqueue(n);
            }
        }
        cache[(home, range)] = dist;
        return dist;
    }

    public static bool InRange(PlanetTile home, PlanetTile tile, int range) => Distances(home, range).ContainsKey(tile);
}
