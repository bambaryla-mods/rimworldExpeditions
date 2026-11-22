// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// Who may go and how a fight on the road turns out.
public static class SquadMath
{
    private const int TicksPerDay = 60000;

    // A hiree goes only if the contract outlasts the whole trip, with a margin for getting out of the map.
    public static bool ContractFits(int ticksLeft, double tripDays, double marginDays) =>
        ticksLeft >= (tripDays + marginDays) * TicksPerDay;

    // Squad strength over enemy strength. Nobody to fight is an easy win; nobody fighting back is a sure loss.
    public static double FightRatio(double squad, double enemy)
    {
        if (enemy <= 0) return double.PositiveInfinity;
        if (squad <= 0) return 0;
        return squad / enemy;
    }

    public static bool SquadWins(double ratio, double roll01, Curve winCurve) =>
        double.IsPositiveInfinity(ratio) || roll01 < winCurve.Evaluate(ratio);
}
