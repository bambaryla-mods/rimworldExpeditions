using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Ideology work sites on the vanilla world map, offered on the board. The mod only reads them, and removes one once a
// squad has raided it.
public static class WorksiteLink
{
    public static bool IsWorksite(Site s) =>
        s.MainSitePartDef is { } def && def.tags != null && def.tags.Contains("WorkSite")
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

    // What raiding it in person would do: the owner turns hostile, the quest ends (Success counts as raiding for
    // Ideology precepts), and the site is gone.
    public static void Raid(Site s, Outcome outcome, ExpeditionReport report)
    {
        if (s.Faction is { } f && !f.IsPlayer && !f.defeated)
        {
            TaggedString text = "";
            SettlementUtility.AffectRelationsOnAttacked(s, ref text);
            if (!text.NullOrEmpty()) report.relationsText = text.Resolve().Trim();
        }
        Quest? quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.State == QuestState.Ongoing
            && q.PartsListForReading.OfType<QuestPart_WorldObjectTimeout>().Any(p => p.worldObject == s));
        quest?.End(outcome == Outcome.Failure ? QuestEndOutcome.Fail : QuestEndOutcome.Success, sendLetter: false);
        if (!s.Destroyed) s.Destroy();
    }

    // The site went away (timed out, or the player is raiding it in person) before the squad got to it: they come
    // home the way they went, with nothing resolved.
    public static bool Lost(Expedition e) =>
        e.opp.site.worksiteOnly && (e.phase == Phase.Outbound || e.phase == Phase.OnSite) && !Alive(e.opp.worksite);

    public static void TurnBack(Expedition e)
    {
        int now = Find.TickManager.TicksGame;
        e.report = new ExpeditionReport { siteLabel = e.opp.Label, tick = now, outcome = Outcome.Gone, hasFinds = false };
        int spent;
        if (e.phase == Phase.Outbound) spent = now - e.phaseStartTick;
        else
        {
            int tiles = e.originMap != null ? Departure.Tiles(e.originMap, e.opp) : (int)Find.WorldGrid.ApproxDistanceInTiles(e.originTile, e.opp.tile);
            spent = (int)(TravelMath.OutDays(e.ride, e.tripFootDays, tiles, ExpeditionsDefOf.Tuning.Travel) * GenDate.TicksPerDay);
        }
        e.opp.worksite = null; // gone or about to be; a saved reference to it would not resolve on load
        e.StartPhase(Phase.Returning, spent);
        ExpeditionsGame.Instance.HolderChanged();
    }

    // One offer for each work site in range that has something to take and isn't offered or raided already.
    public static void Tick()
    {
        ExpeditionSiteDef? def = ExpeditionsDefOf.Exp_WorkSite;
        Map? home = Find.AnyPlayerHomeMap;
        if (def == null || home == null) return;
        var game = ExpeditionsGame.Instance;
        foreach (Site s in Find.WorldObjects.Sites)
        {
            if (!IsWorksite(s) || !Alive(s) || s.parts.Count == 0 || Linked(s)) continue;
            if (!WorldArea.InRange(home.Tile, s.Tile, ExpeditionsMod.Settings.range) || Stash(s).Count == 0) continue;
            var opp = new Opportunity { id = game.NextId(), site = def, tile = s.Tile, worksite = s, faction = s.Faction, expiresTick = -1 };
            game.offers.Add(opp);
            if (Prefs.DevMode) Log.Message($"[Expeditions] Work site offer {opp.id}: {opp.Label} at tile {opp.tile}.");
        }
    }
}
