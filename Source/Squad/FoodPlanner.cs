using RimWorld;
using Verse;
using Verse.AI;

namespace Expeditions;

// Food for the trip: what the squad needs, where it comes from, and what is eaten by the time they are home.
public static class FoodPlanner
{
    public static float DailyNeed(Pawn p) =>
        p.needs?.food == null ? 0f : p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, false) * GenDate.TicksPerDay;

    public static float Required(List<Pawn> squad, double tripDays) =>
        (float)FoodMath.Required(squad.Select(p => (double)DailyNeed(p)).ToList(), tripDays);

    // Food the squad can count on: their own inventories first, then the nearest stockpile food until the need is
    // met. `pickups` is what they must collect on the way out.
    public static float Available(List<Pawn> squad, Map map, float required, out List<(Thing thing, int count)> pickups)
    {
        pickups = new();
        float have = 0f;
        foreach (Pawn p in squad)
            foreach (Thing t in p.inventory?.innerContainer ?? Enumerable.Empty<Thing>())
                if (Edible(t, squad)) have += Nutrition(t) * t.stackCount;
        Pawn? walker = squad.FirstOrDefault(p => p.RaceProps.Humanlike && p.Spawned);
        if (have >= required || walker == null) return have;

        var stock = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
            .Where(t => t.Spawned && !t.IsForbidden(walker) && Edible(t, squad) && walker.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some))
            .OrderByDescending(t => t.def.ingestible?.preferability ?? FoodPreferability.Undefined)
            .ThenBy(t => t.Position.DistanceToSquared(walker.Position));
        foreach (Thing t in stock)
        {
            if (have >= required) break;
            float per = Nutrition(t);
            if (per <= 0f) continue;
            int count = Math.Min(t.stackCount, (int)Math.Ceiling((required - have) / per));
            pickups.Add((t, count));
            have += per * count;
        }
        return have;
    }

    // Removes `nutrition` worth of food from what the returning squad carries, cheapest food first.
    public static void ConsumeOnReturn(List<Pawn> returning, float nutrition)
    {
        var food = returning
            .SelectMany(p => (p.inventory?.innerContainer ?? Enumerable.Empty<Thing>()).ToList())
            .Where(t => Nutrition(t) > 0f)
            .OrderBy(t => t.MarketValue / Nutrition(t))
            .ToList();
        foreach (Thing t in food)
        {
            if (nutrition <= 0f) break;
            float per = Nutrition(t);
            int eat = Math.Min(t.stackCount, (int)Math.Ceiling(nutrition / per));
            nutrition -= eat * per;
            if (eat >= t.stackCount) t.Destroy();
            else t.SplitOff(eat).Destroy();
        }
    }

    private static float Nutrition(Thing t) => t.def.IsNutritionGivingIngestible ? t.GetStatValue(StatDefOf.Nutrition) : 0f;

    private static bool Edible(Thing t, List<Pawn> squad) =>
        t.def.IsNutritionGivingIngestible && !t.def.IsDrug && squad.Any(p => FoodUtility.WillEat(p, t, null, true, false));
}
