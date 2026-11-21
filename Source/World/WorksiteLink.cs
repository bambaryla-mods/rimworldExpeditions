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
