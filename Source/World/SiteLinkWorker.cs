using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// How one family of vanilla quest sites plays on the board: what it holds, how hard it is, and how it ends.
// The defaults read the def's link block; subclasses override only what their family does differently.
public class SiteLinkWorker
{
    public ExpeditionSiteDef def = null!;

    protected SiteLinkProps Props => def.link!;

    public virtual bool Matches(Site s, Quest q) =>
        Props.questRoots.Contains(q.root) && (Props.siteParts.Count == 0 || s.parts.Any(p => Props.siteParts.Contains(p.def)));

    public virtual bool CanOffer(Site s) => true;

    // The site's own threat points: the strongest part's, or the site's total. Some quests give their site none
    // (ancient structures, the mercenaries); those are as strong as a site made now would be.
    public static float Threat(Site s)
    {
        float threat = s.parts.Count == 0 ? 0f : s.parts.Max(p => p.parms?.threatPoints ?? 0f);
        if (threat <= 0f) threat = s.ActualThreatPoints;
        return (float)SiteLinkMath.EffectiveThreat(threat, StorytellerUtility.DefaultSiteThreatPointsNow());
    }

    public virtual float FightRequirement(Site s) =>
        (float)SiteLinkMath.FightRequirement(Threat(s), Props.fightPerThreatPoint, Props.fightMin);

    // The whole prize: the def's table rolled once (no cut on Partial), and the quest's own item rewards.
    public virtual List<Thing> MakeLoot(Expedition e, Outcome outcome)
    {
        var made = new List<Thing>();
        LootRoller.AddTable(made, def.loot, outcome, (e.opp.faction)?.def.techLevel, 1f);
        if (SiteLinks.QuestOf(e.opp.linkedSite!) is { } q)
            foreach (Thing t in RewardItems(q)) made.Add(Copy(t));
        return made;
    }

    // "At best": every sure table entry at its most, and the quest's item rewards.
    public virtual IEnumerable<TaggedString> InfoLines(Opportunity o)
    {
        yield return "Exp_LinkThreat".Translate(Threat(o.linkedSite!).ToString("F0"));
        var parts = new List<string>();
        foreach (LootEntry l in def.loot.Where(l => l.chance >= 1f && l.Live()))
            parts.Add($"{(l.thing?.label ?? l.category!.label)} ×{l.count.max}");
        if (SiteLinks.QuestOf(o.linkedSite!) is { } q)
            parts.AddRange(RewardItems(q).Select(t => $"{t.def.label} ×{t.stackCount}"));
        parts.AddRange(ExtraHaul(o));
        if (parts.Count > 0) yield return "Exp_WorksiteHaul".Translate(string.Join(", ", parts));
    }

    // More of the haul, for subclasses that know of it.
    protected virtual IEnumerable<string> ExtraHaul(Opportunity o) => Enumerable.Empty<string>();

    public bool RemovesOnFailure => !Props.failureKeepsSite;

    // What happens to the site, its quest and its owner once the squad is done. Runs outside the seeded Rand state.
    public virtual void Conclude(Site s, Quest? q, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)
    {
        bool won = outcome != Outcome.Failure;
        if (won && Props.raidCredit) IdeoUtility.Notify_PlayerRaidedSomeone(raiders);
        if (won && q != null)
            foreach (Reward_Goodwill g in Rewards(q).OfType<Reward_Goodwill>())
                if (g.faction is { defeated: false } f && !f.IsPlayer && g.amount != 0)
                    f.TryAffectGoodwillWith(Faction.OfPlayer, g.amount, true, true, null, null);
        if (won) q?.End(QuestEndOutcome.Success, sendLetter: false);
        else if (RemovesOnFailure)
        {
            q?.End(QuestEndOutcome.Fail, sendLetter: false);
            report.siteFateText = "Exp_LinkSiteLost".Translate();
        }
        if ((won || RemovesOnFailure) && !s.Destroyed) s.Destroy();
    }

    // The rewards of the quest's first choice, as the quest window lists them.
    protected static IEnumerable<Reward> Rewards(Quest q) =>
        q.PartsListForReading.OfType<QuestPart_Choice>().FirstOrDefault()?.choices.FirstOrDefault()?.rewards
        ?? Enumerable.Empty<Reward>();

    protected static IEnumerable<Thing> RewardItems(Quest q) =>
        Rewards(q).OfType<Reward_Items>().SelectMany(r => r.items).Where(t => t != null);

    // A new thing like a quest's reward item, so vanilla's own reward is left where it is.
    protected static Thing Copy(Thing t)
    {
        if (t is MinifiedThing { InnerThing: { } inner }) return MinifyUtility.MakeMinified(Copy(inner));
        Thing c = ThingMaker.MakeThing(t.def, t.Stuff);
        c.stackCount = t.stackCount;
        if (t.TryGetQuality(out QualityCategory qc)) c.TryGetComp<CompQuality>()?.SetQuality(qc, ArtGenerationContext.Outsider);
        return c;
    }
}
