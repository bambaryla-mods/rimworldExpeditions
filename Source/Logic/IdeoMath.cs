// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// The rule behind an ideoligion gate: it opens only on a match, and anything it blocks closes it. A gate left with
// nothing to match (every entry from a mod that isn't loaded) stays closed rather than opening for every faith.
public static class IdeoMath
{
    public static bool GateOpen(bool anyMatched, bool blocked) => anyMatched && !blocked;
}
