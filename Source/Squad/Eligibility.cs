using RimWorld;
using Verse;

namespace Expeditions;

// Who may join a squad, and why not. The picker and the launch check both ask here.
// Creepjoiners are never singled out: nothing here may give them away.
public static class Eligibility
{
    public static bool IsChild(Pawn p) => p.DevelopmentalStage == DevelopmentalStage.Child;

    // Babies can't walk out; they are not offered at all.
    public static bool Listed(Pawn p) => !p.RaceProps.Humanlike || !p.DevelopmentalStage.Baby();

    // On a Comm Recruiter contract, and not kept on for good.
    public static bool IsHiree(Pawn p) => CommRecruiterCompat.TryGetHiree(p, out _, out bool retained) && !retained;

    // Someone who keeps slaves in line: one of the player's own free adults. A hiree doesn't count.
    public static bool IsOverlord(Pawn p) =>
        !p.Dead && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsSlave && !p.IsMutant
        && p.DevelopmentalStage.Adult() && !IsHiree(p);

    public static AcceptanceReport CanGo(Pawn p, double tripDays)
    {
        if (!p.RaceProps.Humanlike) return true;
        if (p.IsMutant) return "Exp_NoGoMutant".Translate();
        // A hiree is also a quest guest in vanilla (its contract quest), so it is checked first.
        if (CommRecruiterCompat.TryGetHiree(p, out int ticksLeft, out bool retained))
        {
            if (retained) return true;
            return SquadMath.ContractFits(ticksLeft, Math.Max(tripDays, 0), ExpeditionsDefOf.Tuning.contractMarginDays)
                ? true
                : "Exp_NoGoContract".Translate(Math.Max(ticksLeft, 0).ToStringTicksToPeriod(), Math.Max(tripDays, 0).ToString("F1"));
        }
        if (p.IsQuestLodger()) return "Exp_NoGoGuest".Translate();
        return true;
    }

    public static AcceptanceReport SquadOk(IEnumerable<Pawn> squad)
    {
        var list = squad.ToList();
        if (list.Any(p => p.IsSlave) && !list.Any(IsOverlord)) return "Exp_NeedOverlord".Translate();
        return true;
    }
}
