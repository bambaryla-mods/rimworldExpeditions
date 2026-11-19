// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public readonly record struct FateWeights(double Unharmed, double Injured, double Killed, double Captured);

public static class OutcomeRoll
{
    // The success chance is exactly what the player is shown. Partial takes `partialBand` of the remaining rolls,
    // so even a high chance can still fail outright.
    public static Outcome Roll(double successChance, double roll01, double partialBand = 0.15)
    {
        if (roll01 < successChance) return Outcome.Success;
        if (roll01 < successChance + partialBand * (1 - successChance)) return Outcome.Partial;
        return Outcome.Failure;
    }

    public static PawnFate PickFate(FateWeights w, double roll01)
    {
        double total = w.Unharmed + w.Injured + w.Killed + w.Captured;
        if (total <= 0) return PawnFate.Unharmed;
        double r = roll01 * total;
        if ((r -= w.Unharmed) < 0) return PawnFate.Unharmed;
        if ((r -= w.Injured) < 0) return PawnFate.Injured;
        if ((r -= w.Killed) < 0) return PawnFate.Killed;
        return PawnFate.Captured;
    }
}
