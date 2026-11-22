using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Vanilla quest sites on the world map, offered on the board through site defs with a link block. The mod reads
// them, and its workers end them once a squad is done.
public static class SiteLinks
{
    private static List<ExpeditionSiteDef>? linkDefs;

    private static List<ExpeditionSiteDef> LinkDefs =>
        linkDefs ??= DefDatabase<ExpeditionSiteDef>.AllDefsListForReading
            .Where(d => d.link?.workerClass != null && typeof(SiteLinkWorker).IsAssignableFrom(d.link.workerClass)).ToList();

    // The ongoing quest that spawned the site, found as vanilla does it (Site.CanBeSettled).
    public static Quest? QuestOf(Site s) =>
        Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.State == QuestState.Ongoing
            && q.PartsListForReading.Any(p => p is QuestPart_SpawnWorldObject sp && sp.worldObject == s));

    public static ExpeditionSiteDef? DefFor(Site s, Quest q) => LinkDefs.FirstOrDefault(d => d.LinkWorker!.Matches(s, q));

    // Still there, nobody is in it in person, and its quest is still on. A null link (lost on load) counts as gone.
    public static bool Alive(Site? s) => s != null && s.Spawned && !s.Destroyed && !s.HasMap && QuestOf(s) != null;

    public static bool Linked(Site s)
    {
        var game = ExpeditionsGame.Instance;
        return game.offers.Any(o => o.linkedSite == s) || game.active.Any(e => e.opp.linkedSite == s);
    }

    // The site went away (timed out, done in person, quest over) before the squad was done with it: they come home
    // the way they went, with nothing resolved. A squad still boarding is called off instead (see Tick250).
    public static bool Lost(Expedition e) =>
        e.opp.site.link != null && e.phase != Phase.Returning && !Alive(e.opp.linkedSite);

    public static ExpeditionReport TurnBack(Expedition e, Outcome outcome = Outcome.Gone)
    {
        int now = Find.TickManager.TicksGame;
        e.report = new ExpeditionReport { siteLabel = e.opp.Label, tick = now, outcome = outcome, hasFinds = false };
        var travel = ExpeditionsDefOf.Tuning.Travel;
        int tiles = e.originMap != null ? Departure.Tiles(e.originMap, e.opp) : (int)Find.WorldGrid.ApproxDistanceInTiles(e.originTile, e.opp.tile);
        double outDays = TravelMath.OutDays(e.ride, e.tripFootDays, tiles, travel);
        double soFar = (now - e.phaseStartTick) / (double)GenDate.TicksPerDay;
        // Home the way they went: as long as they have walked so far, or the whole way out if they got there.
        double back = e.phase == Phase.Outbound ? soFar : outDays;
        double away = (e.phase == Phase.Outbound ? soFar : outDays + soFar) + back;
        double trip = TravelMath.TripDays(e.ride, e.tripFootDays, tiles, e.opp.site.daysOnSite, travel);
        e.foodNeeded = (float)WorksiteMath.TurnBackFood(e.foodNeeded, away, trip);
        e.opp.linkedSite = null; // gone, or being done in person; the squad is done with it
        e.StartPhase(Phase.Returning, (int)(back * GenDate.TicksPerDay));
        ExpeditionsGame.Instance.HolderChanged();
        return e.report;
    }

    // Ticks until the site's quest packs it up, or -1 when it has no timer.
    public static int TicksLeft(Site s) =>
        Find.QuestManager.QuestsListForReading
            .Where(q => q.State == QuestState.Ongoing)
            .SelectMany(q => q.PartsListForReading.OfType<QuestPart_WorldObjectTimeout>())
            .FirstOrDefault(p => p.worldObject == s && p.State == QuestPartState.Enabled)?.TicksLeft ?? -1;

    // One offer for each linked site in range that isn't offered or being done already.
    public static void Tick()
    {
        Map? home = Find.AnyPlayerHomeMap;
        if (home == null || LinkDefs.Count == 0) return;
        var game = ExpeditionsGame.Instance;
        foreach (Site s in Find.WorldObjects.Sites)
        {
            if (!s.Spawned || s.Destroyed || s.HasMap || s.parts.Count == 0 || Linked(s)) continue;
            if (!WorldArea.InRange(home.Tile, s.Tile, ExpeditionsMod.Settings.range)) continue;
            Quest? q = QuestOf(s);
            if (q == null || DefFor(s, q) is not { } def || !def.LinkWorker!.CanOffer(s)) continue;
            // The board's deadline is the site's own: when its quest packs it up.
            int left = TicksLeft(s);
            var opp = new Opportunity
            {
                id = game.NextId(), site = def, tile = s.Tile, linkedSite = s,
                faction = s.Faction is { Hidden: false } f ? f : null,
                expiresTick = left < 0 ? -1 : Find.TickManager.TicksGame + left,
            };
            game.offers.Add(opp);
            if (Prefs.DevMode) Log.Message($"[Expeditions] Site offer {opp.id} ({def.defName}): {opp.Label} at tile {opp.tile}.");
        }
    }
}
