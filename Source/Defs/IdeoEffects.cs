using RimWorld;
using Verse;

namespace Expeditions;

// The colony's faith, as the Ideology sites ask about it.
public static class Faith
{
    public static Ideo? Primary => Faction.OfPlayer?.ideos?.PrimaryIdeo;

    // Whether this pawn's own ideoligion passes every ideoligion gate on the site. True for a site with none.
    public static bool PawnMatches(ExpeditionSiteDef site, Pawn p) => site.gates.OfType<SiteGate_Ideo>().All(g => g.Matches(p.Ideo));

    // Who a history event names as its doer: one of the colony's own. A precept's "knows" thought goes only to members
    // of the doer's faction, so a hiree or a captive as doer would give it to nobody.
    public static Pawn? Doer(Expedition e) =>
        e.AwayPawns.FirstOrDefault(p => !p.Dead && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsPrisoner);

    public static void Record(HistoryEventDef def, Pawn? doer) =>
        Find.HistoryEventsManager.RecordEvent(doer != null ? new HistoryEvent(def, doer.Named(HistoryEventArgsNames.Doer)) : new HistoryEvent(def));

    internal static bool CanFeel(Pawn p) => p.needs?.mood?.thoughts?.memories != null;
}

// A mood memory for each pawn that came through the site.
public class PawnEffect_Thought : PawnEffect
{
    public ThoughtDef thought = null!;
    public bool onlyIfFaithMatches;

    public PawnEffect_Thought() => outcomes = new List<Outcome> { Outcome.Success, Outcome.Partial };

    public override void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (!Faith.CanFeel(p) || (onlyIfFaithMatches && !Faith.PawnMatches(e.opp.site, p))) return;
        p.needs.mood.thoughts.memories.TryGainMemory(thought);
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (thought == null) yield return "PawnEffect_Thought has no thought";
    }
}

// Faith grows stronger for each pawn that came through.
public class PawnEffect_Certainty : PawnEffect
{
    public float offset = 0.1f;
    public bool onlyIfFaithMatches = true;

    public PawnEffect_Certainty() => outcomes = new List<Outcome> { Outcome.Success, Outcome.Partial };

    public override void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (p.ideo == null || (onlyIfFaithMatches && !Faith.PawnMatches(e.opp.site, p))) return;
        p.ideo.OffsetCertainty(offset);
        string line = "Exp_CertaintyLine".Translate();
        if (!report.effectLines.Contains(line)) report.effectLines.Add(line);
    }
}

// A mood memory for every colonist at home when the squad gets back. The squad itself isn't on the map yet.
public class OutcomeEffect_ColonyThought : OutcomeEffect
{
    public ThoughtDef thought = null!;
    public bool onlyIfFaithMatches;

    public OutcomeEffect_ColonyThought() => outcomes = new List<Outcome> { Outcome.Success, Outcome.Partial };

    public override void OnArrival(Expedition e, Outcome outcome, Map? map, ExpeditionReport report)
    {
        bool any = false;
        foreach (Pawn p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
        {
            if (!Faith.CanFeel(p) || (onlyIfFaithMatches && !Faith.PawnMatches(e.opp.site, p))) continue;
            p.needs.mood.thoughts.memories.TryGainMemory(thought);
            any = true;
        }
        if (any) report.effectLines.Add("Exp_ColonyThoughtLine".Translate(thought.stages[0].LabelCap));
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (thought == null) yield return "OutcomeEffect_ColonyThought has no thought";
    }
}

// The deed counts as a raid, as vanilla's raiders' do: raiding precepts' moods and the raid clock.
public class OutcomeEffect_RaidCredit : OutcomeEffect
{
    public OutcomeEffect_RaidCredit() => outcomes = new List<Outcome> { Outcome.Success, Outcome.Partial };

    public override void Apply(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        var raiders = e.AwayPawns.Where(p => !p.Dead && p.RaceProps.Humanlike && !p.IsPrisoner).ToList();
        if (raiders.Count == 0) return;
        IdeoUtility.Notify_PlayerRaidedSomeone(raiders);
        report.effectLines.Add("Exp_RaidCreditLine".Translate());
    }
}

// A vanilla history event, for precepts that react to it (charity, conversion).
public class OutcomeEffect_HistoryEvent : OutcomeEffect
{
    public HistoryEventDef eventDef = null!;
    [NoTranslate] public string? lineKey;

    public override void Apply(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        Faith.Record(eventDef, Faith.Doer(e));
        if (!lineKey.NullOrEmpty()) report.effectLines.Add(lineKey.Translate());
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (eventDef == null) yield return "OutcomeEffect_HistoryEvent has no eventDef";
    }
}

// Goodwill with the site's own faction, up or down: a village robbed, a shrine's owners angered.
public class OutcomeEffect_FactionGoodwill : OutcomeEffect
{
    public int amount;

    public override void Apply(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (amount == 0 || e.opp.faction is not { defeated: false } f || f.IsPlayer) return;
        if (f.TryAffectGoodwillWith(Faction.OfPlayer, amount, true, true, null, null))
            report.effectLines.Add("Exp_GoodwillFactionLine".Translate(f.Name, amount.ToStringWithSign()));
    }
}

// Development points for a fluid ideoligion.
public class OutcomeEffect_DevelopmentPoints : OutcomeEffect
{
    public int points = 1;

    public override void Apply(Expedition e, Outcome outcome, ExpeditionReport report)
    {
        if (Faith.Primary is not { Fluid: true } ideo || ideo.development == null) return;
        if (ideo.development.TryAddDevelopmentPoints(points)) report.effectLines.Add("Exp_DevelopmentLine".Translate(points));
    }
}
