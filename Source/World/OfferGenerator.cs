using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Ready-made opportunities: what is actually around the colony, a few at a time, coming and going.
public static class OfferGenerator
{
    private const int TileSamples = 300;
    private const int FactionSiteRadius = 5;

    public static int ReadyMadeCount => ExpeditionsGame.Instance.offers.Count(o => !o.prospected && o.captive == null);

    public static void Tick()
    {
        var game = ExpeditionsGame.Instance;
        int now = Find.TickManager.TicksGame;
        game.offers.RemoveAll(o => o.expiresTick >= 0 && now > o.expiresTick);
        if (now < game.nextOfferTick) return;
        game.nextOfferTick = now + ExpeditionsDefOf.Tuning.offerIntervalTicks.RandomInRange;
        if (ReadyMadeCount >= ExpeditionsMod.Settings.offerSlots) return;
        Map? home = Find.AnyPlayerHomeMap;
        if (home == null) return;
        Opportunity? opp = TryMake(null, home.Tile, null);
        if (opp != null) game.offers.Add(opp);
    }

    // A new opportunity for a site (or a random ready-made one), on a tile in range that fits it.
    public static Opportunity? TryMake(ExpeditionSiteDef? site, PlanetTile home, ThingDef? target)
    {
        int range = ExpeditionsMod.Settings.range;
        List<PlanetTile> tiles = WorldArea.TilesInRange(home, range);
        if (tiles.Count == 0) return null;

        IEnumerable<ExpeditionSiteDef> candidates = site != null
            ? new[] { site }
            : DefDatabase<ExpeditionSiteDef>.AllDefsListForReading.Where(s => !s.rescueOnly && !s.fallback).InRandomOrder();
        foreach (ExpeditionSiteDef s in candidates)
        {
            if (!TryFindTile(s, home, tiles, out PlanetTile tile, out Faction? faction)) continue;
            var tuning = ExpeditionsDefOf.Tuning;
            return new Opportunity
            {
                id = ExpeditionsGame.Instance.NextId(),
                site = s,
                tile = tile,
                faction = faction,
                target = target,
                prospected = target != null,
                expiresTick = Find.TickManager.TicksGame
                    + (target != null ? tuning.prospectLifetimeTicks : tuning.offerLifetimeTicks).RandomInRange,
            };
        }
        return null;
    }

    public static bool SiteFits(ExpeditionSiteDef site, PlanetTile tile, out Faction? faction)
    {
        faction = null;
        Tile t = Find.WorldGrid[tile];
        if (t.WaterCovered) return false;
        if (site.biomes.Count > 0 && !site.biomes.Contains(t.PrimaryBiome)) return false;
        if (site.hilliness.Count > 0 && !site.hilliness.Contains(t.HillinessLabel)) return false;
        if (site.needsRiver && (t as SurfaceTile)?.Rivers.NullOrEmpty() != false) return false;
        if (site.needsRoad && (t as SurfaceTile)?.Roads.NullOrEmpty() != false) return false;
        if (site.needsCoast && !Find.World.CoastDirectionAt(tile).IsValid) return false;
        if (site.needsFaction == FactionNeed.None) return true;
        Settlement? s = Find.WorldObjects.Settlements
            .Where(x => Wants(site.needsFaction, x.Faction) && Find.WorldGrid.ApproxDistanceInTiles(x.Tile, tile) <= FactionSiteRadius)
            .RandomElementWithFallback();
        faction = s?.Faction;
        return s != null;
    }

    public static bool Wants(FactionNeed need, Faction? f)
    {
        if (f == null || f.IsPlayer || f.Hidden || f.defeated) return false;
        return need switch
        {
            FactionNeed.Hostile => f.HostileTo(Faction.OfPlayer),
            FactionNeed.Ally => f.PlayerRelationKind == FactionRelationKind.Ally,
            _ => true,
        };
    }

    private static bool TryFindTile(ExpeditionSiteDef site, PlanetTile home, List<PlanetTile> tiles, out PlanetTile tile, out Faction? faction)
    {
        faction = null;
        tile = PlanetTile.Invalid;
        IEnumerable<PlanetTile> pool = tiles;
        if (site.needsFaction != FactionNeed.None)
        {
            // Only tiles near a qualifying settlement in range; far cheaper than testing random tiles.
            var near = Find.WorldObjects.Settlements
                .Where(s => Wants(site.needsFaction, s.Faction) && WorldArea.InRange(home, s.Tile, ExpeditionsMod.Settings.range))
                .SelectMany(s => tiles.Where(t => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, t) <= FactionSiteRadius))
                .ToList();
            if (near.Count == 0) return false;
            pool = near;
        }
        foreach (PlanetTile t in pool.InRandomOrder().Take(TileSamples))
        {
            if (t == home || !SiteFits(site, t, out faction)) continue;
            tile = t;
            return true;
        }
        return false;
    }
}
