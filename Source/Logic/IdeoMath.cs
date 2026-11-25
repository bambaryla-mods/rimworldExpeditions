// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// The rule behind an ideoligion gate: anything it blocks closes it; otherwise it opens on a match, or when it lists
// nothing to match at all (a gate made only of blocks).
public static class IdeoMath
{
    public static bool GateOpen(bool anyListed, bool anyMatched, bool blocked) => !blocked && (!anyListed || anyMatched);
}
