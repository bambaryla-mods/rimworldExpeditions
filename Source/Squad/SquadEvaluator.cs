using RimWorld;
using Verse;

namespace Expeditions;

// What one pawn's stat added to a challenge. The explanation is vanilla's stat breakdown, so it names gear,
// bionics, traits and skills.
public record Contribution(Pawn pawn, StatDef stat, float value, string explanation);

public record ChallengeResult(SiteChallenge site, double score, double chance, List<Contribution> parts);

public record SquadEvaluation(List<ChallengeResult> challenges, double success, double find);

// A ride and whether it can be used now. Filled in by RideFinder.
public record RideOption(Ride ride, bool available, string? reason, float cargoKg, int armedCount, List<Thing> things);

public static class SquadEvaluator
{
    private const int ExplanationLines = 8;

    public static SquadEvaluation Evaluate(IReadOnlyList<Pawn> pawns, ExpeditionSiteDef site, Ride ride, RideOption? rideOption, int seed)
    {
        ExpeditionTuningDef tuning = ExpeditionsDefOf.Tuning;
        var results = new List<ChallengeResult>();
        foreach (SiteChallenge sc in site.challenges)
        {
            ExpeditionChallengeDef def = sc.challenge;
            var parts = new List<Contribution>();
            var scores = new List<double>();
            foreach (Pawn pawn in pawns)
            {
                // Pack animals only help carry.
                if (!pawn.RaceProps.Humanlike && !def.isHaul) continue;
                var terms = new List<StatTerm>();
                foreach (ChallengeStat cs in def.stats)
                {
                    float value = cs.stat.Worker.IsDisabledFor(pawn) ? 0f : pawn.GetStatValue(cs.stat);
                    terms.Add(new StatTerm(value, cs.weight, cs.reference));
                    parts.Add(new Contribution(pawn, cs.stat, value, Explain(pawn, cs.stat, value)));
                }
                scores.Add(ChallengeMath.PawnScore(terms));
            }

            double score = ChallengeMath.Combine(scores, def.combine);
            if (def.isHaul && rideOption != null && (ride == Ride.Shuttle || ride == Ride.Vehicles))
                score += rideOption.cargoKg / 75f * 100f;
            if (def.isStealth)
                score = ChallengeMath.StealthCap(score, ride, tuning.loudStealthCap);
            if (def == ExpeditionsDefOf.Exp_Fight && ride == Ride.Vehicles && rideOption != null && rideOption.armedCount > 0)
                score += ChallengeMath.ArmedBonus(tuning.armedFightBonus * rideOption.armedCount, site.openness,
                    ProspectSeed.Roll01(seed, 0, "armed"), tuning.armedSpread);

            results.Add(new ChallengeResult(sc, score, ChallengeMath.ChallengeChance(score, sc.requirement, tuning.RatioCurve), parts));
        }

        double success = ChallengeMath.SuccessChance(results.Select(r => (r.chance, (double)r.site.weight)).ToList());
        var search = results.Where(r => r.site.challenge.isSearch).ToList();
        double find = search.Count == 0
            ? site.findChanceBase
            : Math.Clamp(site.findChanceBase * (0.5 + 0.5 * search.Max(r => r.chance)), 0.05, 0.95);
        return new SquadEvaluation(results, success, find);
    }

    private static string Explain(Pawn pawn, StatDef stat, float value)
    {
        string full = stat.Worker.GetExplanationFull(StatRequest.For(pawn), stat.toStringNumberSense, value);
        var lines = full.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).Take(ExplanationLines);
        return $"{pawn.LabelShortCap}: {stat.LabelCap} {stat.ValueToString(value)}\n" + string.Join("\n", lines);
    }
}
