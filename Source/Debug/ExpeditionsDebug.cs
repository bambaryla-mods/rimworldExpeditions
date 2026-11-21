using LudeonTK;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
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

    [DebugAction("Expeditions", "Spawn offer of...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnOfferOf()
    {
        var options = DefDatabase<ExpeditionSiteDef>.AllDefsListForReading
            .Where(s => !s.FromWorldOnly)
            .Select(site => new FloatMenuOption(site.label, () =>
            {
                Opportunity? opp = OfferGenerator.TryMake(site, Find.CurrentMap.Tile, null);
                if (opp == null)
                {
                    Log.Warning($"[Expeditions] {site.defName} fits no tile in range (terrain, faction, enemy or season).");
                    return;
                }
                ExpeditionsGame.Instance.offers.Add(opp);
                Log.Message($"[Expeditions] Offer {opp.id}: {opp.Label} at tile {opp.tile}.");
            }))
            .ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    // Fills the board past the usual slot limit: some random offers, or one of every site that fits somewhere.
    [DebugAction("Expeditions", "Spawn many offers...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnManyOffers()
    {
        var options = new List<FloatMenuOption>();
        foreach (int n in new[] { 5, 10, 25 })
            options.Add(new FloatMenuOption($"{n} random", () => SpawnOffers(Enumerable.Repeat<ExpeditionSiteDef?>(null, n))));
        options.Add(new FloatMenuOption("one of every site", () =>
            SpawnOffers(DefDatabase<ExpeditionSiteDef>.AllDefsListForReading.Where(s => !s.FromWorldOnly))));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void SpawnOffers(IEnumerable<ExpeditionSiteDef?> sites)
    {
        PlanetTile home = Find.CurrentMap.Tile;
        var made = new List<string>();
        var misfits = new List<string>();
        foreach (ExpeditionSiteDef? site in sites)
        {
            Opportunity? opp = OfferGenerator.TryMake(site, home, null);
            if (opp == null)
            {
                misfits.Add(site?.defName ?? "random");
                continue;
            }
            ExpeditionsGame.Instance.offers.Add(opp);
            made.Add(opp.Label);
        }
        Log.Message($"[Expeditions] Spawned {made.Count} offers: {string.Join(", ", made)}");
        if (misfits.Count > 0)
            Log.Warning($"[Expeditions] No tile in range fits (terrain, faction, enemy or season): {string.Join(", ", misfits)}");
        Messages.Message($"Spawned {made.Count} offers" + (misfits.Count > 0 ? $", {misfits.Count} fit nowhere (see log)" : ""),
            MessageTypeDefOf.NeutralEvent, false);
    }

    [DebugAction("Expeditions", "Finish prospect for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FinishProspect()
    {
        var options = ProspectCatalog.All
            .Select(d => new FloatMenuOption($"{d.label} ({ProspectCatalog.Chance(d):P0})", () => ProspectService.Complete(d, Find.CurrentMap)))
            .ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction("Expeditions", "Force next outcome...", allowedGameStates = AllowedGameStates.Playing)]
    private static void ForceOutcome() =>
        Find.WindowStack.Add(new FloatMenu(Enum.GetValues<Outcome>()
            .Select(o => new FloatMenuOption(o.ToString(), () => Resolver.forcedOutcome = o)).ToList()));

    [DebugAction("Expeditions", "Advance expedition phase...", allowedGameStates = AllowedGameStates.Playing)]
    private static void AdvancePhase()
    {
        var options = ExpeditionsGame.Instance.active
            .Select(e => new FloatMenuOption($"{e.id}: {e.opp.Label} ({e.phase})", () =>
            {
                e.phaseEndTick = Find.TickManager.TicksGame;
                ExpeditionsGame.Instance.Tick250();
            }))
            .ToList();
        if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction("Expeditions", "Dump map bake (both styles)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void DumpMap()
    {
        MapBaker.ClearCache();
        foreach (MapStyle style in Enum.GetValues<MapStyle>())
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            BakedMap map = MapBaker.Get(Find.CurrentMap.Tile, ExpeditionsMod.Settings.range, style);
            watch.Stop();
            string path = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, $"Expeditions_map_{style}.png");
            System.IO.File.WriteAllBytes(path, map.texture.EncodeToPNG());
            Log.Message($"[Expeditions] {style} bake: {watch.ElapsedMilliseconds} ms, {map.texPixel.Count} tiles -> {path}");
        }
    }

    private static void LogEvaluation(List<Pawn> pawns, ExpeditionSiteDef site)
    {
        SquadEvaluation eval = SquadEvaluator.Evaluate(pawns, new Opportunity { id = 0, site = site }, Ride.Foot, null);
        var sb = new System.Text.StringBuilder($"[Expeditions] {site.defName} on foot: success {eval.success:P0}, find {eval.find:P0}\n");
        foreach (ChallengeResult r in eval.challenges)
        {
            sb.AppendLine($"  {r.site.challenge.defName}: score {r.score:F0} / {r.requirement:F0} -> {r.chance:P0}");
            foreach (Contribution c in r.parts) sb.AppendLine("    " + c.explanation.Replace("\n", "\n    "));
        }
        Log.Message(sb.ToString());
    }
}
