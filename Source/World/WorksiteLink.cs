using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Ideology work sites on the vanilla world map, offered on the board. The mod only reads them, and removes one once a
// squad has raided it.
public static class WorksiteLink
{
    public static bool IsWorksite(Site s) =>
        s.parts.Count > 0 && s.MainSitePartDef is { } def && def.tags != null && def.tags.Contains("WorkSite")
        && typeof(SitePartWorker_WorkSite).IsAssignableFrom(def.workerClass);

    // Still there and nobody is raiding it in person. A null link (lost on load) counts as gone.
    public static bool Alive(Site? s) => s != null && s.Spawned && !s.Destroyed && !s.HasMap;

    // The stash, summed per item.
    public static List<ThingDefCountClass> Stash(Site s)
    {
        var sums = new Dictionary<ThingDef, int>();
        foreach (Thing t in (IEnumerable<Thing>?)s.parts[0].things ?? Enumerable.Empty<Thing>())
            sums[t.def] = (sums.TryGetValue(t.def, out int had) ? had : 0) + t.stackCount;
        return sums.Select(kv => new ThingDefCountClass(kv.Key, kv.Value)).ToList();
    }

    public static int Guards(Site s) => s.parts[0].expectedEnemyCount;

    public static bool Linked(Site s)
    {
        var game = ExpeditionsGame.Instance;
        return game.offers.Any(o => o.worksite == s) || game.active.Any(e => e.opp.worksite == s);
    }

    // What raiding it in person would do: the owner turns hostile, the quest ends, and the site is gone. A Success or
    // Partial is a raid for Ideology: the squad gets the credit as vanilla's raiders do (precept moods, raid clock).
    public static void Raid(Site s, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)
    {
        if (outcome != Outcome.Failure) IdeoUtility.Notify_PlayerRaidedSomeone(raiders);
        if (s.Faction is { } f && !f.IsPlayer && !f.defeated)
        {
            TaggedString text = "";
            SettlementUtility.AffectRelationsOnAttacked(s, ref text);
            if (!text.NullOrEmpty()) report.relationsText = text.Resolve().Trim();
        }
        Timeout(s)?.quest?.End(outcome == Outcome.Failure ? QuestEndOutcome.Fail : QuestEndOutcome.Success, sendLetter: false);
        if (!s.Destroyed) s.Destroy();
    }

    // The site went away (timed out, or the player is raiding it in person) before the squad got to it: they come
    // home the way they went, with nothing resolved.
    // A squad still boarding is called off instead (see ExpeditionsGame.Tick250).
    public static bool Lost(Expedition e) =>
        e.opp.site.worksiteOnly && e.phase != Phase.Returning && !Alive(e.opp.worksite);

    public static void TurnBack(Expedition e)
    {
        int now = Find.TickManager.TicksGame;
        e.report = new ExpeditionReport { siteLabel = e.opp.Label, tick = now, outcome = Outcome.Gone, hasFinds = false };
        var travel = ExpeditionsDefOf.Tuning.Travel;
        int tiles = e.originMap != null ? Departure.Tiles(e.originMap, e.opp) : (int)Find.WorldGrid.ApproxDistanceInTiles(e.originTile, e.opp.tile);
        double outDays = TravelMath.OutDays(e.ride, e.tripFootDays, tiles, travel);
        double soFar = (now - e.phaseStartTick) / (double)GenDate.TicksPerDay;
        // Home the way they went: as long as they have walked so far, or the whole way out if they got there.
        double back = e.phase == Phase.Outbound ? soFar : outDays;
        double away = (e.phase == Phase.Outbound ? soFar : outDays + soFar) + back;
        double trip = TravelMath.TripDays(e.ride, e.tripFootDays, tiles, e.opp.site.daysOnSite, travel);
        e.foodNeeded = (float)WorksiteMath.TurnBackFood(e.foodNeeded, away, trip);
        e.opp.worksite = null; // gone, or being raided in person; the squad is done with it
        e.StartPhase(Phase.Returning, (int)(back * GenDate.TicksPerDay));
        ExpeditionsGame.Instance.HolderChanged();
    }

    // Ticks until the site's quest packs it up, or -1 when it has no timer.
    public static int TicksLeft(Site s) => Timeout(s)?.TicksLeft ?? -1;

    private static QuestPart_WorldObjectTimeout? Timeout(Site s) =>
        Find.QuestManager.QuestsListForReading
            .Where(q => q.State == QuestState.Ongoing)
            .SelectMany(q => q.PartsListForReading.OfType<QuestPart_WorldObjectTimeout>())
            .FirstOrDefault(p => p.worldObject == s);

    // One offer for each work site in range that has something to take and isn't offered or raided already.
    public static void Tick()
    {
        ExpeditionSiteDef? def = ExpeditionsDefOf.Exp_WorkSite;
        Map? home = Find.AnyPlayerHomeMap;
        if (def == null || home == null) return;
        var game = ExpeditionsGame.Instance;
        foreach (Site s in Find.WorldObjects.Sites)
        {
            if (!IsWorksite(s) || !Alive(s) || Linked(s)) continue;
            if (!WorldArea.InRange(home.Tile, s.Tile, ExpeditionsMod.Settings.range) || Stash(s).Count == 0) continue;
            // The board's deadline is the site's own: when its quest packs it up.
            int left = TicksLeft(s);
            var opp = new Opportunity { id = game.NextId(), site = def, tile = s.Tile, worksite = s, faction = s.Faction,
                expiresTick = left < 0 ? -1 : Find.TickManager.TicksGame + left };
            game.offers.Add(opp);
            if (Prefs.DevMode) Log.Message($"[Expeditions] Work site offer {opp.id}: {opp.Label} at tile {opp.tile}.");
        }
    }
}
