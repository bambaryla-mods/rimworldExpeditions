// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// One stat of one pawn for one challenge: its value, its weight in the challenge, and the value a decent pawn has.
public readonly record struct StatTerm(double Value, double Weight, double Reference);

public static class ChallengeMath
{
    // No single stat may count for more than twice a decent pawn's, so one superstat cannot carry a challenge.
    public const double TermCap = 2.0;

    // 100 means "a decent pawn" on every stat of the challenge.
    public static double PawnScore(IReadOnlyList<StatTerm> terms)
    {
        double sum = 0, weights = 0;
        foreach (var t in terms)
        {
            double ratio = t.Reference > 0 ? Math.Min(t.Value / t.Reference, TermCap) : 0;
            sum += t.Weight * ratio;
            weights += t.Weight;
        }
        return weights > 0 ? 100 * sum / weights : 0;
    }

    public static double Combine(IReadOnlyList<double> scores, CombineRule rule)
    {
        if (scores.Count == 0) return 0;
        return rule switch
        {
            CombineRule.Sum => scores.Sum(),
            CombineRule.Min => scores.Min(),
            CombineRule.Max => scores.Max(),
            CombineRule.Average => scores.Average(),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
    }

    public static double ChallengeChance(double score, double requirement, Curve ratioCurve) =>
        requirement <= 0 ? 1 : ratioCurve.Evaluate(score / requirement);

    public static double SuccessChance(IReadOnlyList<(double chance, double weight)> parts, double min = 0.05, double max = 0.95)
    {
        double sum = 0, weights = 0;
        foreach (var (chance, weight) in parts)
        {
            sum += chance * weight;
            weights += weight;
        }
        if (weights <= 0) return max;
        return Math.Clamp(sum / weights, min, max);
    }

    // An armed vehicle's fight bonus: scaled by how open the site is, then spread up or down by the roll.
    public static double ArmedBonus(double baseBonus, double openness, double roll01, double spread) =>
        baseBonus * openness * (1 + spread * (2 * roll01 - 1));

    // A loud arrival (anything but walking in) gives the squad away.
    public static double StealthCap(double score, Ride ride, double loudCap) =>
        ride == Ride.Foot ? score : Math.Min(score, loudCap);
}
