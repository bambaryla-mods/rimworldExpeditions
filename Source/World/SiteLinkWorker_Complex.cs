using RimWorld;
using Verse;

namespace Expeditions;

// Ancient complexes: on top of the def's table, vanilla's own room loot makers, rolled as the complex's rooms would be.
public class SiteLinkWorker_Complex : SiteLinkWorker
{
    public override List<Thing> MakeLoot(Expedition e, Outcome outcome)
    {
        List<Thing> made = base.MakeLoot(e, outcome);
        foreach (ThingSetMakerDef maker in Props.lootMakers)
            for (int i = 0; i < Props.lootMakerRolls; i++)
                made.AddRange(maker.root.Generate(new ThingSetMakerParams()));
        return made;
    }

    // A loot maker can't be previewed reliably (it reads the world as it is when it runs), so the line only names it.
    protected override IEnumerable<string> ExtraHaul(Opportunity o)
    {
        if (Props.lootMakers.Count > 0) yield return "Exp_LinkComplexLoot".Translate();
    }
}
