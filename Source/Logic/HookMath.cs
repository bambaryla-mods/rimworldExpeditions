// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// Numbers behind the hooks other mods use: faction payment, joiners on a partial win, capped kills.
public static class HookMath
{
    // Silver paid in goods: the value is shared evenly between the kinds, each count rounded down, the rest in silver.
    public static (int[] counts, int silverLeft) SplitPayment(double silver, IReadOnlyList<double> unitValues)
    {
        var counts = new int[unitValues.Count];
        if (unitValues.Count == 0 || silver <= 0) return (counts, (int)Math.Max(0, silver));
        double share = silver / unitValues.Count;
        double spent = 0;
        for (int i = 0; i < unitValues.Count; i++)
        {
            if (unitValues[i] <= 0) continue;
            counts[i] = (int)Math.Floor(share / unitValues[i]);
            spent += counts[i] * unitValues[i];
        }
        return (counts, (int)Math.Floor(silver - spent));
    }

    public static double JoinerChance(double chance, Outcome outcome, double partialFactor) => outcome switch
    {
        Outcome.Success => chance,
        Outcome.Partial => chance * partialFactor,
        _ => 0,
    };

    public static int HalfUp(int n) => (n + 1) / 2;

    // The most of `add` kills that keeps crystallization (podThreshold x kills / killsToPod) below a full brain.
    public static int CappedKills(int have, int add, int killsToPod, double podThreshold)
    {
        if (add <= 0 || killsToPod <= 0 || podThreshold <= 0) return Math.Max(0, add);
        int maxTotal = (int)Math.Ceiling(killsToPod / podThreshold) - 1;
        return Math.Max(0, Math.Min(add, maxTotal - have));
    }
}
