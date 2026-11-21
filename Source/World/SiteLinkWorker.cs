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

    public virtual float FightRequirement(Site s) => Props.fightMin;

    // What the squad brings back, before the carry limit.
    public virtual List<Thing> MakeLoot(Expedition e, Outcome outcome) => new();

    // Extra rows for the offer panel.
    public virtual IEnumerable<TaggedString> InfoLines(Opportunity o) => Enumerable.Empty<TaggedString>();

    public bool RemovesOnFailure => !Props.failureKeepsSite;

    // What happens to the site, its quest and its owner once the squad is done. Runs outside the seeded Rand state.
    public virtual void Conclude(Site s, Quest? q, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)
    {
    }
}
