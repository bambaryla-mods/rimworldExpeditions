using RimWorld;
using Verse;

namespace Expeditions;

// What one pawn's stat or skill added to a challenge. For a stat the explanation is vanilla's stat breakdown, so it
// names gear, bionics, traits and skills.
public record Contribution(Pawn pawn, string label, float value, string explanation);

public record ChallengeResult(SiteChallenge site, double requirement, double score, double chance, List<Contribution> parts);

public record SquadEvaluation(List<ChallengeResult> challenges, double success, double find);

// A ride and whether it can be used now. Filled in by RideFinder.
public record RideOption(Ride ride, bool available, string? reason, float cargoKg, int armedCount, List<Thing> things);

public static class SquadEvaluator
{
    private const int ExplanationLines = 8;

    public static SquadEvaluation Evaluate(IReadOnlyList<Pawn> pawns, Opportunity opp, Ride ride, RideOption? rideOption)
    {
        ExpeditionSiteDef site = opp.site;
        int seed = opp.Seed;
        ExpeditionTuningDef tuning = ExpeditionsDefOf.Tuning;
        var results = new List<ChallengeResult>();
        foreach (SiteChallenge sc in site.challenges)
        {
            ExpeditionChallengeDef def = sc.challenge;
            var parts = new List<Contribution>();
            double score = Score(pawns, def, parts);
            if (def.isHaul && rideOption != null && (ride == Ride.Shuttle || ride == Ride.Vehicles))
                score += rideOption.cargoKg / 75f * 100f;
            if (def.isStealth)
                score = ChallengeMath.StealthCap(score, ride, tuning.loudStealthCap);
            if (def == ExpeditionsDefOf.Exp_Fight && ride == Ride.Vehicles && rideOption != null && rideOption.armedCount > 0)
                score += ChallengeMath.ArmedBonus(tuning.armedFightBonus * rideOption.armedCount, site.openness,
                    ProspectSeed.Roll01(seed, 0, "armed"), tuning.armedSpread);

            double requirement = opp.Requirement(sc);
            results.Add(new ChallengeResult(sc, requirement, score, ChallengeMath.ChallengeChance(score, requirement, tuning.RatioCurve), parts));
        }

        double success = ChallengeMath.SuccessChance(results.Select(r => (r.chance, (double)r.site.weight)).ToList());
        var search = results.Where(r => r.site.challenge.isSearch).ToList();
        double find = site.link != null ? 1 // a linked site's prize is known: there is nothing to miss
            : search.Count == 0
            ? site.findChanceBase
            : Math.Clamp(site.findChanceBase * (0.5 + 0.5 * search.Max(r => r.chance)), 0.05, 0.95);
        return new SquadEvaluation(results, success, find);
    }

    // The squad's score on one challenge, from the pawns alone (no ride or vehicle bonuses). Pack animals only help carry.
    public static double Score(IEnumerable<Pawn> pawns, ExpeditionChallengeDef def, List<Contribution>? parts = null)
    {
        var scores = new List<double>();
        foreach (Pawn pawn in pawns)
        {
            if (!pawn.RaceProps.Humanlike && !def.isHaul) continue;
            var terms = new List<StatTerm>();
            Contribution? last = null;
            foreach (ChallengeStat cs in def.stats)
            {
                Contribution part = cs.stat != null ? StatPart(pawn, cs.stat) : SkillPart(pawn, cs.skill!);
                terms.Add(new StatTerm(part.value, cs.weight, cs.reference));
                parts?.Add(part);
                last = part;
            }
            double pawnScore = ChallengeMath.PawnScore(terms);
            // A child is a liability: weaker in a fight and carries less, on top of vanilla's own penalties.
            if (def.childFactor != 1f && Eligibility.IsChild(pawn))
            {
                pawnScore *= def.childFactor;
                if (parts != null && last != null)
                    parts[parts.Count - 1] = last with { explanation = last.explanation + "\n" + "Exp_ChildFactor".Translate(def.childFactor.ToString("0.##")) };
            }
            scores.Add(pawnScore);
        }
        return ChallengeMath.Combine(scores, def.combine);
    }

    private static Contribution StatPart(Pawn pawn, StatDef stat)
    {
        float value = stat.Worker.IsDisabledFor(pawn) ? 0f : pawn.GetStatValue(stat);
        return new Contribution(pawn, stat.LabelCap, value, Explain(pawn, stat, value));
    }

    // The skill level as the value; a skill the pawn can't use counts as 0.
    private static Contribution SkillPart(Pawn pawn, SkillDef skill)
    {
        SkillRecord? record = pawn.skills?.GetSkill(skill);
        float value = record == null || record.TotallyDisabled ? 0f : record.Level;
        string passion = record?.passion switch
        {
            Passion.Minor => " " + "Exp_PassionMinor".Translate(),
            Passion.Major => " " + "Exp_PassionMajor".Translate(),
            _ => "",
        };
        return new Contribution(pawn, skill.LabelCap, value, $"{pawn.LabelShortCap}: {skill.LabelCap} {value:F0}{passion}");
    }

    private static string Explain(Pawn pawn, StatDef stat, float value)
    {
        string full = stat.Worker.GetExplanationFull(StatRequest.For(pawn), stat.toStringNumberSense, value);
        var lines = full.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).Take(ExplanationLines);
        return $"{pawn.LabelShortCap}: {stat.LabelCap} {stat.ValueToString(value)}\n" + string.Join("\n", lines);
    }
}
