using RimWorld;
using Verse;

namespace Expeditions;

// Real fight wounds on an away pawn, already tended by the squad, so nobody bleeds out on the walk home.
public static class InjuryMaker
{
    private const float DamageAtFullSeverity = 40f;
    private static HediffDef[]? kinds;
    private static HediffDef[] Kinds => kinds ??= new[] { "Gunshot", "Cut", "Bruise" }.Select(n => DefDatabase<HediffDef>.GetNamed(n)).ToArray();

    public static List<string> Apply(Pawn pawn, float severity01, bool allowPartLoss)
    {
        var notes = new List<string>();
        var parts = pawn.health.hediffSet.GetNotMissingParts()
            .Where(p => p.depth == BodyPartDepth.Outside && p.def != BodyPartDefOf.Head && p.def != BodyPartDefOf.Neck
                && p != pawn.RaceProps.body.corePart && !p.def.tags.Any(t => t.vital))
            .ToList();
        if (parts.Count == 0) return notes;

        float budget = severity01 * DamageAtFullSeverity;
        int wounds = Rand.RangeInclusive(1, 4);
        for (int i = 0; i < wounds && budget > 0.5f; i++)
        {
            BodyPartRecord part = parts.RandomElement();
            float partHealth = pawn.health.hediffSet.GetPartHealth(part);
            float damage = Math.Min(budget, Rand.Range(4f, 12f));
            if (!allowPartLoss) damage = Math.Min(damage, partHealth - 1f);
            if (damage <= 0.5f) continue;
            budget -= damage;

            if (allowPartLoss && damage >= partHealth)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, part), part);
                parts.RemoveAll(p => p == part || p.parent == part);
                notes.Add("Exp_InjuryLost".Translate(part.LabelShort));
                if (parts.Count == 0) break;
                continue;
            }
            var injury = (Hediff_Injury)HediffMaker.MakeHediff(Kinds.RandomElement(), pawn, part);
            injury.Severity = damage;
            pawn.health.AddHediff(injury, part);
            injury.Tended(Rand.Range(0.4f, 0.8f), 1f);
            notes.Add($"{injury.LabelCap} ({part.LabelShort})");
        }
        return notes;
    }
}
