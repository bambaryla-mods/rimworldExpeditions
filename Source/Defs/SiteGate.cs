using RimWorld;
using Verse;

namespace Expeditions;

// A condition a site waits on before it can be offered (and stays on the board only while it holds). Other mods add
// their own as <li Class="...">: a path chosen, a quest open, a mechanitor present. A closed gate never touches a
// squad already away.
public abstract class SiteGate
{
    public abstract bool Open();

    // Why it is closed, for the debug evaluator.
    public virtual string? ClosedReason => null;

    // When the offer must be gone by (an absolute tick), for sites answering something with its own clock.
    public virtual int? DeadlineTick => null;

    public virtual IEnumerable<string> ConfigErrors() { yield break; }
}

// Research finished: every project in `research`, and at least one in `anyResearch` when it has any.
public class SiteGate_Research : SiteGate
{
    public List<ResearchProjectDef> research = new();
    public List<ResearchProjectDef> anyResearch = new();

    public override bool Open() =>
        research.All(r => r == null || r.IsFinished) && (anyResearch.Count == 0 || anyResearch.Any(r => r != null && r.IsFinished));

    public override string? ClosedReason =>
        string.Join(", ", research.Concat(anyResearch).Where(r => r != null && !r.IsFinished).Select(r => r.label));

    public override IEnumerable<string> ConfigErrors()
    {
        if (research.Count == 0 && anyResearch.Count == 0) yield return "SiteGate_Research lists no research";
    }
}

// The colony has a mechanitor, at home or out on an expedition (Biotech).
public class SiteGate_Mechanitor : SiteGate
{
    public override bool Open() =>
        ModsConfig.BiotechActive && (PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Any(MechanitorUtility.IsMechanitor)
            || ExpeditionsGame.Instance.AllAwayPawns.Any(p => !p.Dead && MechanitorUtility.IsMechanitor(p)));

    public override string? ClosedReason => "no mechanitor";
}

// The colony's main ideoligion wants this (Ideology). Any entry in the "any" lists opens it; anything in the "no"
// lists closes it whatever else matches. Entries from other DLCs and mods carry MayRequire and drop out when absent;
// a gate left with nothing to match stays closed.
public class SiteGate_Ideo : SiteGate
{
    public List<PreceptDef> anyPrecept = new();
    public List<MemeDef> anyMeme = new();
    public bool approvesSlavery;
    public List<RitualPatternDef> anyRitualPattern = new();
    public List<PreceptDef> noPrecept = new();
    public List<MemeDef> noMeme = new();

    private bool AnyListed => anyPrecept.Count > 0 || anyMeme.Count > 0 || approvesSlavery || anyRitualPattern.Count > 0;

    public bool Matches(Ideo? ideo)
    {
        if (ideo == null) return false;
        return IdeoMath.GateOpen(AnyMatch(ideo), Block(ideo) != null);
    }

    private bool AnyMatch(Ideo ideo) =>
        anyPrecept.Any(p => p != null && ideo.HasPrecept(p))
        || anyMeme.Any(m => m != null && ideo.HasMeme(m))
        || (approvesSlavery && ideo.PreceptsListForReading.Any(p => p.def.approvesOfSlavery))
        || (anyRitualPattern.Count > 0 && ideo.PreceptsListForReading.OfType<Precept_Ritual>().Any(r => anyRitualPattern.Contains(r.sourcePattern)));

    // The first thing in this ideoligion that rules the site out, if any.
    private Def? Block(Ideo ideo) =>
        (Def?)noPrecept.FirstOrDefault(p => p != null && ideo.HasPrecept(p)) ?? noMeme.FirstOrDefault(m => m != null && ideo.HasMeme(m));

    public override bool Open() => ModsConfig.IdeologyActive && Matches(Faction.OfPlayer?.ideos?.PrimaryIdeo);

    public override string? ClosedReason
    {
        get
        {
            Ideo? ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (!ModsConfig.IdeologyActive || ideo == null) return "no ideoligion";
            return Block(ideo) is { } b ? $"ruled out by {b.label}" : "no matching ideoligion";
        }
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (!AnyListed && noPrecept.Count == 0 && noMeme.Count == 0) yield return "SiteGate_Ideo lists nothing to match";
    }
}
