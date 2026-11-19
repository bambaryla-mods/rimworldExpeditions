using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

public record CaptiveInfo(Pawn pawn, Faction captor, int heldTicks);

// Every colonist held by another faction, whether taken on an expedition or kidnapped by raiders, read from
// vanilla's KidnappedPawnsTracker so vanilla's own handling (selling, recruiting, raids) still applies.
public static class CaptiveTracker
{
    public static IEnumerable<CaptiveInfo> All()
    {
        var game = ExpeditionsGame.Instance;
        int now = Find.TickManager.TicksGame;
        foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
        {
            if (f.IsPlayer || f.kidnapped == null) continue;
            foreach (Pawn p in f.kidnapped.KidnappedPawnsListForReading)
            {
                if (p.Dead || !p.IsColonist) continue;
                if (!game.capturedTick.TryGetValue(p, out int since)) game.capturedTick[p] = since = now;
                yield return new CaptiveInfo(p, f, now - since);
            }
        }
    }

    public static bool IsHeld(Pawn p) =>
        Find.FactionManager.AllFactionsListForReading.Any(f => f.kidnapped?.KidnappedPawnsListForReading.Contains(p) == true);

    public static bool HasRescue(Pawn p) =>
        ExpeditionsGame.Instance.offers.Any(o => o.captive == p) || ExpeditionsGame.Instance.active.Any(e => e.opp.captive == p);

    // A holding camp: the captor's nearest settlement in range, or a camp on a random tile in range.
    public static Opportunity? CreateRescue(CaptiveInfo c, PlanetTile home)
    {
        int range = ExpeditionsMod.Settings.range;
        Settlement? settlement = Find.WorldObjects.Settlements
            .Where(s => s.Faction == c.captor && WorldArea.InRange(home, s.Tile, range))
            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(home, s.Tile))
            .FirstOrDefault();
        PlanetTile tile = settlement?.Tile ?? WorldArea.TilesInRange(home, range).Where(t => t != home).RandomElementWithFallback(PlanetTile.Invalid);
        if (!tile.Valid) return null;
        var opp = new Opportunity
        {
            id = ExpeditionsGame.Instance.NextId(),
            site = ExpeditionsDefOf.Exp_HoldingCamp,
            tile = tile,
            faction = c.captor,
            captive = c.pawn,
            expiresTick = Find.TickManager.TicksGame + ExpeditionsDefOf.Tuning.prospectLifetimeTicks.RandomInRange,
        };
        ExpeditionsGame.Instance.offers.Add(opp);
        return opp;
    }

    // Takes a captive back from whoever holds them. Kidnapping keeps the pawn's faction, so nothing to restore.
    public static bool Free(Pawn p)
    {
        Faction? captor = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.kidnapped?.KidnappedPawnsListForReading.Contains(p) == true);
        if (captor == null) return false;
        captor.kidnapped.RemoveKidnappedPawn(p);
        if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
        ExpeditionsGame.Instance.capturedTick.Remove(p);
        return true;
    }
}
