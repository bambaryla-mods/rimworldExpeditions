using RimWorld;
using Verse;
using Verse.AI;

namespace Expeditions;

// What a squad carries to give away at the site (alms): food on top of the trip's own, and medicine.
public static class SupplyPlanner
{
    // The trip's food plus the food to give away: checked and picked up as one, so the alms aren't eaten on the way.
    public static float FoodNeed(List<Pawn> squad, double tripDays, ExpeditionSiteDef site) =>
        FoodPlanner.Required(squad, tripDays) + (site.supplies?.nutrition ?? 0f);

    // Medicine the squad can count on: their own inventories first, then the nearest stock until the count is met.
    public static int Medicine(List<Pawn> squad, Map map, int count, out List<(Thing thing, int count)> pickups)
    {
        pickups = new();
        int have = 0;
        foreach (Pawn p in squad)
            foreach (Thing t in p.inventory?.innerContainer ?? Enumerable.Empty<Thing>())
                if (t.def.IsMedicine) have += t.stackCount;
        Pawn? walker = squad.FirstOrDefault(p => p.RaceProps.Humanlike && p.Spawned);
        if (have >= count || walker == null) return have;

        var stock = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine)
            .Where(t => t.Spawned && !t.IsForbidden(walker) && walker.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some))
            .OrderBy(t => t.MarketValue)
            .ThenBy(t => t.Position.DistanceToSquared(walker.Position)); // cheapest first: nobody gives away glitterworld medicine by accident
        foreach (Thing t in stock)
        {
            if (have >= count) break;
            int take = Math.Min(t.stackCount, count - have);
            pickups.Add((t, take));
            have += take;
        }
        return have;
    }

    // Gives away up to this much food and medicine from what the carriers hold, cheapest first. Returns what was given.
    public static (float nutrition, int medicine) Spend(List<Pawn> carriers, float nutrition, int medicine)
    {
        float food = nutrition > 0f ? FoodPlanner.ConsumeOnReturn(carriers, nutrition) : 0f;
        int given = 0;
        var meds = carriers
            .SelectMany(p => (p.inventory?.innerContainer ?? Enumerable.Empty<Thing>()).ToList())
            .Where(t => t.def.IsMedicine)
            .OrderBy(t => t.MarketValue)
            .ToList();
        foreach (Thing t in meds)
        {
            if (given >= medicine) break;
            int take = Math.Min(t.stackCount, medicine - given);
            given += take;
            if (take >= t.stackCount) t.Destroy();
            else t.SplitOff(take).Destroy();
        }
        return (food, given);
    }
}
