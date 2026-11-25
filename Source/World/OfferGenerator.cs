using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Ready-made opportunities: what is actually around the colony, a few at a time, coming and going.
public static class OfferGenerator
{
    private const int TileSamples = 300;
    private const int FactionSiteRadius = 5;
    private const int EnemySiteRadius = 12; // the enemy only needs to be in striking distance, not next door
    private const float GrowingTempMin = 6f;
    private const float GrowingTempMax = 42f;

    public static int ReadyMadeCount => ExpeditionsGame.Instance.offers.Count(o => !o.prospected && o.captive == null && o.site.link == null && !o.site.immediate);

    public static void Tick()
    {
        var game = ExpeditionsGame.Instance;
        int now = Find.TickManager.TicksGame;
        game.offers.RemoveAll(o => (o.expiresTick >= 0 && now > o.expiresTick) || !StillFits(o));
        SiteLinks.Tick();
        ImmediateTick();
        if (now < game.nextOfferTick) return;
        game.nextOfferTick = now + ExpeditionsDefOf.Tuning.offerIntervalTicks.RandomInRange;
        if (ReadyMadeCount >= ExpeditionsMod.Settings.offerSlots) return;
        Map? home = Find.AnyPlayerHomeMap;
        if (home == null) return;
        Opportunity? opp = TryMake(null, home.Tile, null);
        if (opp != null) game.offers.Add(opp);
    }

    // Sites that answer something with its own clock (a quest that ends in days): offered the moment their gates open,
    // outside the slots and the timer, one offer at a time and none while a squad is out on one.
    private static void ImmediateTick()
    {
        var game = ExpeditionsGame.Instance;
        Map? home = Find.AnyPlayerHomeMap;
        if (home == null) return;
        foreach (ExpeditionSiteDef s in DefDatabase<ExpeditionSiteDef>.AllDefsListForReading)
        {
            if (!s.immediate || s.FromWorldOnly) continue;
            if (game.offers.Any(o => o.site == s) || game.active.Any(e => e.opp.site == s) || !s.GatesOpen) continue;
            Opportunity? opp = TryMake(s, home.Tile, null);
            if (opp == null) continue;
            if (opp.linkedSettlement != null) game.offers.RemoveAll(o => o.linkedSettlement == opp.linkedSettlement && !o.site.immediate);
            game.offers.Add(opp);
        }
    }

    // An offer whose patron is no longer friendly enough, or whose enemy made peace, is withdrawn.
    private static bool StillFits(Opportunity o)
    {
        if (o.captive != null) return true;
        if (!o.site.GatesOpen) return false;
        if (o.site.settlementLink != null && !SettlementLinks.Alive(o)) return false;
        if (o.site.link != null) return SiteLinks.Alive(o.linkedSite);
        if (o.site.needsFaction != FactionNeed.None && !Wants(o.site.needsFaction, o.faction, o.site.factionDefs)) return false;
        if (o.site.needsEnemy && !Wants(FactionNeed.Hostile, o.enemy, o.site.enemyFactionDefs)) return false;
        return true;
    }

    // A new opportunity for a site (or a random ready-made one), on a tile in range that fits it.
    public static Opportunity? TryMake(ExpeditionSiteDef? site, PlanetTile home, ThingDef? target)
    {
        int range = ExpeditionsMod.Settings.range;
        List<PlanetTile> tiles = WorldArea.TilesInRange(home, range);
        if (tiles.Count == 0) return null;

        IEnumerable<ExpeditionSiteDef> candidates = site != null ? new[] { site } : ReadyMadeOrder();
        foreach (ExpeditionSiteDef s in candidates)
        {
            PlanetTile tile;
            Faction? faction, enemy;
            Settlement? settlement = null;
            if (s.settlementLink != null)
            {
                if (!TryFindSettlement(s, home, out settlement, out faction, out enemy)) continue;
                tile = settlement!.Tile;
            }
            else if (!TryFindTile(s, home, tiles, out tile, out faction, out enemy)) continue;
            var tuning = ExpeditionsDefOf.Tuning;
            int? deadline = s.GateDeadline;
            return new Opportunity
            {
                id = ExpeditionsGame.Instance.NextId(),
                site = s,
                tile = tile,
                faction = faction,
                enemy = enemy,
                target = target,
                linkedSettlement = settlement,
                prospected = target != null,
                expiresTick = deadline ?? Find.TickManager.TicksGame
                    + (target != null ? tuning.prospectLifetimeTicks : tuning.offerLifetimeTicks).RandomInRange,
            };
        }
        return null;
    }

    // A settlement in range for a settlement-linked site. With a patron (needsFaction), the patron is found near the
    // settlement and the settlement's owner is the enemy; otherwise the owner is the offer's faction.
    private static bool TryFindSettlement(ExpeditionSiteDef s, PlanetTile home, out Settlement? settlement, out Faction? faction, out Faction? enemy)
    {
        settlement = null;
        faction = null;
        enemy = null;
        foreach (Settlement st in SettlementLinks.Candidates(s, home).InRandomOrder())
        {
            if (s.needsFaction != FactionNeed.None)
            {
                Faction? patron = NearbyFaction(s.needsFaction, st.Tile, st.Faction, EnemySiteRadius, s.factionDefs);
                if (patron == null) continue;
                faction = patron;
                enemy = st.Faction;
            }
            else
            {
                faction = st.Faction;
                enemy = null;
            }
            settlement = st;
            return true;
        }
        return false;
    }

    // Ready-made sites in a random order weighted by commonness, so a family of similar sites (the tournaments)
    // doesn't crowd the board.
    private static IEnumerable<ExpeditionSiteDef> ReadyMadeOrder()
    {
        var sites = DefDatabase<ExpeditionSiteDef>.AllDefsListForReading.Where(s => !s.FromWorldOnly && !s.fallback && !s.immediate && s.GatesOpen).ToList();
        return WeightedOrder.Order(sites.Select(s => (double)s.commonness).ToList(), () => Rand.Value).Select(i => sites[i]);
    }

    public static bool SiteFits(ExpeditionSiteDef site, PlanetTile tile, out Faction? faction, out Faction? enemy)
    {
        faction = null;
        enemy = null;
        Tile t = Find.WorldGrid[tile];
        if (t.WaterCovered) return false;
        if (site.biomes.Count > 0 && !site.biomes.Contains(t.PrimaryBiome)) return false;
        if (site.hilliness.Count > 0 && !site.hilliness.Contains(t.HillinessLabel)) return false;
        if (site.needsRiver && (t as SurfaceTile)?.Rivers.NullOrEmpty() != false) return false;
        if (site.needsRoad && (t as SurfaceTile)?.Roads.NullOrEmpty() != false) return false;
        if (site.needsCoast && !Find.World.CoastDirectionAt(tile).IsValid) return false;
        if (site.growingSeason && !GrowingSeason(tile)) return false;
        if (site.tamedAnimals != null && site.tamedAnimals.Candidates(t.PrimaryBiome).Count == 0) return false;
        if (site.needsFaction != FactionNeed.None)
        {
            faction = NearbyFaction(site.needsFaction, tile, null, FactionSiteRadius, site.factionDefs);
            if (faction == null) return false;
        }
        if (site.needsEnemy)
        {
            enemy = NearbyFaction(FactionNeed.Hostile, tile, faction, EnemySiteRadius, site.enemyFactionDefs);
            if (enemy == null) return false;
        }
        return true;
    }

    // The season's average temperature in the band where crops grow well. The season, not this hour, so a cold night
    // doesn't hide a harvest.
    private static bool GrowingSeason(PlanetTile tile)
    {
        float temp = Find.World.tileTemperatures.GetSeasonalTemp(tile);
        return temp >= GrowingTempMin && temp <= GrowingTempMax;
    }

    // Same layer only: settlements in orbit (Odyssey) have tile ids of another layer, and measuring across layers errors.
    private static Faction? NearbyFaction(FactionNeed need, PlanetTile tile, Faction? not, int radius, List<FactionDef> defs) =>
        Find.WorldObjects.Settlements
            .Where(x => x.Tile.Layer == tile.Layer && x.Faction != not && Wants(need, x.Faction, defs)
                && Find.WorldGrid.ApproxDistanceInTiles(x.Tile, tile) <= radius)
            .RandomElementWithFallback()?.Faction;

    // As below, and of one of these defs when any are listed.
    public static bool Wants(FactionNeed need, Faction? f, List<FactionDef>? defs) =>
        Wants(need, f) && (defs == null || defs.Count == 0 || defs.Contains(f!.def));

    public static bool Wants(FactionNeed need, Faction? f)
    {
        if (f == null || f.IsPlayer || f.Hidden || f.defeated) return false;
        return need switch
        {
            FactionNeed.Hostile => f.HostileTo(Faction.OfPlayer),
            FactionNeed.Ally => f.PlayerRelationKind == FactionRelationKind.Ally,
            FactionNeed.Friendly => !f.HostileTo(Faction.OfPlayer),
            _ => true,
        };
    }

    private static bool TryFindTile(ExpeditionSiteDef site, PlanetTile home, List<PlanetTile> tiles, out PlanetTile tile,
        out Faction? faction, out Faction? enemy)
    {
        faction = null;
        enemy = null;
        tile = PlanetTile.Invalid;
        IEnumerable<PlanetTile> pool = tiles;
        FactionNeed anchor = site.needsFaction != FactionNeed.None ? site.needsFaction : site.needsEnemy ? FactionNeed.Hostile : FactionNeed.None;
        List<FactionDef> anchorDefs = site.needsFaction != FactionNeed.None ? site.factionDefs : site.enemyFactionDefs;
        if (anchor != FactionNeed.None)
        {
            // Only tiles near a qualifying settlement in range; far cheaper than testing random tiles.
            var near = Find.WorldObjects.Settlements
                .Where(s => Wants(anchor, s.Faction, anchorDefs) && WorldArea.InRange(home, s.Tile, ExpeditionsMod.Settings.range))
                .SelectMany(s => tiles.Where(t => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, t) <= FactionSiteRadius))
                .ToList();
            if (near.Count == 0) return false;
            pool = near;
        }
        foreach (PlanetTile t in pool.InRandomOrder().Take(TileSamples))
        {
            if (t == home || !SiteFits(site, t, out faction, out enemy)) continue;
            tile = t;
            return true;
        }
        return false;
    }
}
