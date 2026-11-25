using Verse;

namespace Expeditions;

// Lets another mod strike things out of what a squad brings home: subclass it, and Expeditions finds it at startup.
// It runs on the rolled loot before the haul is weighed, and again when the squad gets home, so a rule that changed on
// the way back still holds. That second pass sees everything carried home besides people and the ride: the haul, the
// patron's payment, and whatever supplies the squad packed. Return a line for the report, or null.
public abstract class ExpeditionLootFilter
{
    // Remove what the filter forbids from `loot`. Things removed are destroyed by Expeditions.
    public abstract string? Filter(Expedition e, List<Thing> loot);
}

public static class LootFilters
{
    private static List<ExpeditionLootFilter>? all;

    public static List<ExpeditionLootFilter> All => all ??= GenTypes.AllSubclassesNonAbstract(typeof(ExpeditionLootFilter))
        .Select(t => Effects.Safe(ExpeditionsDefOf.Exp_Rumor, () => (ExpeditionLootFilter?)Activator.CreateInstance(t), null))
        .Where(f => f != null).Cast<ExpeditionLootFilter>().ToList();

    // Runs every filter over `loot` in place. Struck things are destroyed; report lines go to the report.
    public static void Run(Expedition e, List<Thing> loot, ExpeditionReport report)
    {
        foreach (ExpeditionLootFilter f in All)
        {
            var kept = loot.ToList();
            string? line = Effects.Safe(e.opp.site, () => f.Filter(e, kept), null);
            foreach (Thing t in loot.Where(t => !kept.Contains(t)).ToList())
            {
                loot.Remove(t);
                if (!t.Destroyed) t.Destroy();
            }
            if (!line.NullOrEmpty() && !report.effectLines.Contains(line!)) report.effectLines.Add(line!);
        }
    }
}
