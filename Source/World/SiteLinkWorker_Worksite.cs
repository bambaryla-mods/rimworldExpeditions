using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Ideology work sites: the squad takes a cut of the known stash, the guards set the fight, and the site is gone
// whatever the outcome.
public class SiteLinkWorker_Worksite : SiteLinkWorker
{
    public static bool IsWorksite(Site s) =>
        s.parts.Count > 0 && s.MainSitePartDef is { } d && d.tags != null && d.tags.Contains("WorkSite")
        && typeof(SitePartWorker_WorkSite).IsAssignableFrom(d.workerClass);

    // The stash, summed per item.
    public static List<ThingDefCountClass> Stash(Site s)
    {
        var sums = new Dictionary<ThingDef, int>();
        foreach (Thing t in (IEnumerable<Thing>?)s.parts[0].things ?? Enumerable.Empty<Thing>())
            sums[t.def] = (sums.TryGetValue(t.def, out int had) ? had : 0) + t.stackCount;
        return sums.Select(kv => new ThingDefCountClass(kv.Key, kv.Value)).ToList();
    }

    public static int Guards(Site s) => s.parts[0].expectedEnemyCount;

    public override bool Matches(Site s, Quest q) => IsWorksite(s) && base.Matches(s, q);

    public override bool CanOffer(Site s) => Stash(s).Count > 0;

    public override float FightRequirement(Site s) =>
        (float)WorksiteMath.FightRequirement(Guards(s), ExpeditionsDefOf.Tuning.worksiteFightPerGuard);

    public override List<Thing> MakeLoot(Expedition e, Outcome outcome)
    {
        var made = new List<Thing>();
        float share = ExpeditionsDefOf.Tuning.worksiteShare;
        foreach (ThingDefCountClass s in Stash(e.opp.linkedSite!))
        {
            int n = WorksiteMath.Share(s.count, share, outcome);
            if (n > 0) made.AddRange(LootRoller.Make(s.thingDef, n));
        }
        return made;
    }

    // The guards, the most the squad can bring back of each stash item, and a warning when the owner isn't hostile yet.
    public override IEnumerable<TaggedString> InfoLines(Opportunity o)
    {
        Site s = o.linkedSite!;
        yield return "Exp_WorksiteGuards".Translate(Guards(s));
        float share = ExpeditionsDefOf.Tuning.worksiteShare;
        string haul = string.Join(", ", Stash(s)
            .Select(x => (x.thingDef, n: WorksiteMath.Share(x.count, share, Outcome.Success)))
            .Where(x => x.n > 0)
            .Select(x => $"{x.thingDef.label} ×{x.n}"));
        yield return "Exp_WorksiteHaul".Translate(haul);
        if (o.faction != null && !o.faction.HostileTo(Faction.OfPlayer))
            yield return "Exp_WorksiteTurnsHostile".Translate(o.faction.Name).Colorize(ColorLibrary.RedReadable);
    }

    // What raiding it in person would do: the owner turns hostile, the quest ends, and the site is gone. A Success or
    // Partial is a raid for Ideology: the squad gets the credit as vanilla's raiders do (precept moods, raid clock).
    public override void Conclude(Site s, Quest? q, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)
    {
        if (outcome != Outcome.Failure) IdeoUtility.Notify_PlayerRaidedSomeone(raiders);
        if (s.Faction is { } f && !f.IsPlayer && !f.defeated)
        {
            TaggedString text = "";
            SettlementUtility.AffectRelationsOnAttacked(s, ref text);
            if (!text.NullOrEmpty()) report.relationsText = text.Resolve().Trim();
        }
        q?.End(outcome == Outcome.Failure ? QuestEndOutcome.Fail : QuestEndOutcome.Success, sendLetter: false);
        if (!s.Destroyed) s.Destroy();
    }
}
