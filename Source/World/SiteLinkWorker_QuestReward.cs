using RimWorld;
using Verse;

namespace Expeditions;

// Sites whose quest names its prize without making it (Odyssey's gravcore sites): vanilla only places the item when
// the site's map is generated, so the squad's haul is made here, with counts from the def.
public class SiteLinkWorker_QuestReward : SiteLinkWorker
{
    public override List<Thing> MakeLoot(Expedition e, Outcome outcome)
    {
        List<Thing> made = base.MakeLoot(e, outcome);
        if (SiteLinks.QuestOf(e.opp.linkedSite!) is { } q)
            foreach ((ThingDef d, int n) in Listed(q)) made.AddRange(LootRoller.Make(d, n));
        return made;
    }

    protected override IEnumerable<string> ExtraHaul(Opportunity o) =>
        SiteLinks.QuestOf(o.linkedSite!) is { } q ? Listed(q).Select(x => $"{x.def.label} ×{x.count}") : Enumerable.Empty<string>();

    private IEnumerable<(ThingDef def, int count)> Listed(Quest q) =>
        Rewards(q).OfType<Reward_DefinedThingDef>().Where(r => r.thingDef != null)
            .Select(r => (r.thingDef, Props.rewardCounts.FirstOrDefault(c => c.thingDef == r.thingDef)?.count ?? 1));
}
