using RimWorld;
using Verse;

namespace Expeditions;

// What a squad brings back: rolled from the site, then cut to what they can carry home.
public static class LootRoller
{
    public static List<Thing> Roll(Expedition e, Outcome outcome, bool found, ExpeditionReport report)
    {
        var made = new List<Thing>();
        if (outcome == Outcome.Failure) return made;
        ExpeditionSiteDef site = e.opp.site;
        float factor = outcome == Outcome.Partial ? 0.5f : 1f;

        // A linked vanilla site: its worker says what is there. Its prize is known, so it is always found.
        if (site.LinkWorker is { } worker && SiteLinks.Alive(e.opp.linkedSite))
            return Fit(e, worker.MakeLoot(e, outcome), report);

        if (site.silverReward > 0) Add(made, ThingDefOf.Silver, Scale(site.silverReward, factor));
        if (found)
        {
            // The loot is whoever holds the site: the enemy on a job for a patron, otherwise the site's faction.
            TechLevel? limit = (e.opp.enemy ?? e.opp.faction)?.def.techLevel;
            AddTable(made, site.loot, outcome, limit, factor);
            if (e.opp.target != null) Add(made, e.opp.target, Scale(e.opp.targetCount, factor));
        }
        return Fit(e, made, report);
    }

    // One roll of a loot table: each entry by its chance, a category picking one eligible item.
    public static void AddTable(List<Thing> made, List<LootEntry> loot, Outcome outcome, TechLevel? limit, float factor)
    {
        foreach (LootEntry entry in loot)
        {
            if (entry.successOnly && outcome != Outcome.Success) continue;
            if (!Rand.Chance(entry.chance)) continue;
            ThingDef? def = entry.thing ?? Pick(entry.category!, limit);
            if (def != null) Add(made, def, Scale(entry.count.RandomInRange, factor));
        }
    }

    private static int Scale(int count, float factor) => Math.Max(1, (int)Math.Round(count * factor));

    private static ThingDef? Pick(ThingCategoryDef category, TechLevel? limit) =>
        category.DescendantThingDefs
            .Where(d => ProspectCatalog.Eligible(d) && (limit == null || d.techLevel <= limit))
            .RandomElementWithFallback();

    // Things of one def, split into stacks, quality rolled as a trader's would be.
    public static List<Thing> Make(ThingDef def, int count)
    {
        var made = new List<Thing>();
        Add(made, def, count);
        return made;
    }

    private static void Add(List<Thing> made, ThingDef def, int count)
    {
        // A building only travels packed up; one that can't be (no minified form) isn't loot.
        if (def.category == ThingCategory.Building)
        {
            if (def.minifiedDef == null)
            {
                Log.Warning($"[Expeditions] {def.defName} is a building that can't be packed up; left out of the loot.");
                return;
            }
            for (int i = 0; i < count; i++) made.Add(MinifyUtility.MakeMinified(ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def))));
            return;
        }
        while (count > 0)
        {
            Thing t = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            t.stackCount = Math.Min(count, def.stackLimit);
            t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(), ArtGenerationContext.Outsider);
            count -= t.stackCount;
            made.Add(t);
        }
    }

    // Keeps the most valuable per kilogram that fits; the rest is left behind and recorded.
    private static List<Thing> Fit(Expedition e, List<Thing> made, ExpeditionReport report)
    {
        float capacity = e.AwayPawns.Sum(p => Math.Max(0f, MassUtility.Capacity(p, null) - MassUtility.GearAndInventoryMass(p)));
        if (e.ride == Ride.Shuttle || e.ride == Ride.Vehicles) capacity += RideFinder.HeldCargo(e);

        var stacks = made.Select(t => new LootStack(t.MarketValue, t.GetStatValue(StatDefOf.Mass), t.stackCount)).ToList();
        int[] kept = LootFit.Fit(stacks, capacity);
        var result = new List<Thing>();
        for (int i = 0; i < made.Count; i++)
        {
            Thing t = made[i];
            int lost = t.stackCount - kept[i];
            if (lost > 0) report.lostToHaul.Add(new ThingDefCountClass(t.def, lost));
            if (kept[i] <= 0)
            {
                t.Destroy();
                continue;
            }
            t.stackCount = kept[i];
            report.loot.Add(new ThingDefCountClass(t.def, kept[i]));
            result.Add(t);
        }
        return result;
    }
}
