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
