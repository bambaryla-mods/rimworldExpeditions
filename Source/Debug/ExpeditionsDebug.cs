using LudeonTK;
using RimWorld;
using Verse;

namespace Expeditions;

public static class ExpeditionsDebug
{
    [DebugAction("Expeditions", "Evaluate selected pawns vs site...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void EvaluateSelected()
    {
        var pawns = Find.Selector.SelectedPawns.ToList();
        if (pawns.Count == 0)
        {
            Messages.Message("Select pawns first.", MessageTypeDefOf.RejectInput, false);
            return;
        }
        var options = DefDatabase<ExpeditionSiteDef>.AllDefsListForReading
            .Select(site => new FloatMenuOption(site.label, () => LogEvaluation(pawns, site)))
            .ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void LogEvaluation(List<Pawn> pawns, ExpeditionSiteDef site)
    {
        SquadEvaluation eval = SquadEvaluator.Evaluate(pawns, site, Ride.Foot, null, 0);
        var sb = new System.Text.StringBuilder($"[Expeditions] {site.defName} on foot: success {eval.success:P0}, find {eval.find:P0}\n");
        foreach (ChallengeResult r in eval.challenges)
        {
            sb.AppendLine($"  {r.site.challenge.defName}: score {r.score:F0} / {r.site.requirement:F0} -> {r.chance:P0}");
            foreach (Contribution c in r.parts) sb.AppendLine("    " + c.explanation.Replace("\n", "\n    "));
        }
        Log.Message(sb.ToString());
    }
}
