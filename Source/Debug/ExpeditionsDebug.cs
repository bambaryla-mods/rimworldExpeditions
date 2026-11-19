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

    [DebugAction("Expeditions", "Send selected pawns away (test)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SendAway()
    {
        Map map = Find.CurrentMap;
        var pawns = Find.Selector.SelectedPawns.Where(p => p.IsColonist).ToList();
        if (pawns.Count == 0) return;
        var game = ExpeditionsGame.Instance;
        var e = new Expedition
        {
            id = game.NextId(),
            opp = new Opportunity { id = game.NextId(), site = ExpeditionsDefOf.Exp_Rumor, tile = map.Tile },
            ride = Ride.Foot,
            originMap = map,
            originTile = map.Tile,
        };
        e.StartPhase(Phase.Outbound, int.MaxValue / 2);
        e.squad.AddRange(pawns);
        game.active.Add(e);
        foreach (Pawn p in pawns) e.Take(p);
        Log.Message($"[Expeditions] Test expedition {e.id}: {pawns.Count} pawns away.");
    }

    [DebugAction("Expeditions", "Bring all back (test)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void BringAllBack()
    {
        Map map = Find.CurrentMap;
        var game = ExpeditionsGame.Instance;
        foreach (Expedition e in game.active.ToList())
        {
            foreach (Thing t in e.contents.ToList()) e.Release(t, map, map.Center);
            game.active.Remove(e);
        }
    }

    [DebugAction("Expeditions", "Spawn offer", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnOffer()
    {
        Opportunity? opp = OfferGenerator.TryMake(null, Find.CurrentMap.Tile, null);
        if (opp == null)
        {
            Log.Warning("[Expeditions] No site fits any tile in range.");
            return;
        }
        ExpeditionsGame.Instance.offers.Add(opp);
        Log.Message($"[Expeditions] Offer {opp.id}: {opp.site.defName} at tile {opp.tile}, {TravelEstimator.NominalFootDays(Find.CurrentMap.Tile, opp.tile):F1} days on foot.");
    }

    [DebugAction("Expeditions", "Finish prospect for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FinishProspect()
    {
        var options = ProspectCatalog.All
            .Select(d => new FloatMenuOption($"{d.label} ({ProspectCatalog.Chance(d):P0})", () => ProspectService.Complete(d, Find.CurrentMap)))
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
