using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// Coming home: pick a map, eat the trip's food out of their packs, set them down, and file the report.
public static class Arrival
{
    public static void Return(Expedition e)
    {
        var game = ExpeditionsGame.Instance;
        game.active.Remove(e);
        ExpeditionReport report = e.report ?? new ExpeditionReport { siteLabel = e.opp.Label, tick = Find.TickManager.TicksGame };
        game.AddHistory(report);

        var pawns = e.AwayPawns.Where(p => !p.Dead).ToList();
        // A squad wiped out at the site, or robbed of everything on the road, is lost with its animals. Animals whose
        // people were only called away (a trip cut short) find their own way home.
        bool unescorted = e.unescorted && report.lostText == null && pawns.Count > 0;
        if (!unescorted && !pawns.Any(p => p.RaceProps.Humanlike && !RideFinder.VehicleCompat.IsVehicle(p)))
        {
            // Animals left without their people die with the expedition (so bonds and relations resolve properly);
            // vehicles and cargo are lost.
            foreach (Thing t in e.contents.ToList())
            {
                e.contents.Remove(t);
                if (t is Pawn p && !p.Dead && !RideFinder.VehicleCompat.IsVehicle(p))
                {
                    if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                    p.Kill(null);
                }
                else if (!t.Destroyed) t.Destroy();
            }
            Find.LetterStack.ReceiveLetter("Exp_LostLabel".Translate(), report.lostText ?? "Exp_LostText".Translate(report.siteLabel), LetterDefOf.NegativeEvent);
            game.HolderChanged();
            return;
        }

        FoodPlanner.ConsumeOnReturn(pawns.Where(p => !RideFinder.VehicleCompat.IsVehicle(p)).ToList(), e.foodNeeded);

        Map? map = e.originMap != null && Find.Maps.Contains(e.originMap) && e.originMap.IsPlayerHome ? e.originMap : Find.AnyPlayerHomeMap;
        Retaliation.Hand(e, map); // somebody came home, as a squad or a caravan: the raid owed starts counting down
        // What they carry, checked again: a rule may have changed on the way home. Never the ride or the people.
        var carried = e.contents.Where(t => t is not Pawn && t is not Building).ToList();
        var keep = carried.ToList();
        LootFilters.Run(e, keep, report);
        foreach (Thing t in carried.Where(t => !keep.Contains(t) && e.contents.Contains(t))) e.contents.Remove(t); // destroying usually removes it already
        foreach (OutcomeEffect oe in Resolver.OutcomeEffectsFor(e).Where(x => x.outcomes.Contains(report.outcome)))
            Effects.Safe(e.opp.site, () => oe.OnArrival(e, report.outcome, map, report));
        if (map == null)
        {
            BecomeCaravan(e, pawns);
        }
        else
        {
            IntVec3 cell = ArrivalCell(e, map);
            // Ride first (a shuttle lands), then people and animals, then the cargo next to them.
            foreach (Thing t in e.contents.Where(t => t is Building).ToList())
            {
                e.contents.Remove(t);
                // The spot is clear when one was found; if not, stacks are moved aside rather than erased.
                GenSpawn.Spawn(t, cell, map, Rot4.North, WipeMode.VanishOrMoveAside);
                FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, 3f, UnityEngine.Color.gray);
            }
            foreach (Pawn p in e.contents.OfType<Pawn>().ToList()) e.Release(p, map, cell);
            foreach (Thing t in e.contents.ToList()) e.Release(t, map, cell);
            var letter = (ChoiceLetter_ExpeditionReport)LetterMaker.MakeLetter("Exp_ReturnedLabel".Translate(report.siteLabel), ReportText(report),
                ExpeditionsDefOf.Exp_ReportLetter, new LookTargets(pawns.Where(p => p.Spawned)));
            letter.reportTick = report.tick;
            Find.LetterStack.ReceiveLetter(letter);
        }
        game.HolderChanged();
    }

    private static IntVec3 ArrivalCell(Expedition e, Map map)
    {
        if (e.ride == Ride.Shuttle && e.contents.OfType<Building>().FirstOrDefault() is { } shuttle) return ShuttleCell(shuttle.def.Size, map);
        if (CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c),
                map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 edge))
            return edge;
        return DropCellFinder.TradeDropSpot(map);
    }

    // Where the shuttle sets down. Spawning a building erases whatever its footprint covers (walls, furniture, stacks),
    // and a drop pod's spot check lets items and many buildings through, so the spot has to be clear for the shuttle's
    // own size. A landing beacon zone first, then vanilla's safe shuttle spot near the colony (no items or buildings
    // under it, away from hostiles), then the same without the hostile check.
    private static IntVec3 ShuttleCell(IntVec2 size, Map map)
    {
        if (DropCellFinder.TryFindShipLandingArea(map, out IntVec3 zone, out _) && DropCellFinder.SkyfallerCanLandAt(zone, map, size)) return zone;
        IntVec3 spot = DropCellFinder.TryFindSafeLandingSpotCloseToColony(map, size, Faction.OfPlayer);
        if (spot.IsValid) return spot;
        spot = DropCellFinder.TryFindSafeLandingSpotCloseToColony(map, size);
        if (spot.IsValid) return spot;
        Log.Warning("[Expeditions] No clear spot for the returning shuttle; landing at the trade drop spot.");
        return DropCellFinder.TradeDropSpot(map);
    }

    // No player home map left: they become an ordinary caravan on the tile they left from, so nobody is lost.
    public static void BecomeCaravan(Expedition e, List<Pawn> pawns)
    {
        foreach (Pawn p in pawns) e.contents.Remove(p);
        Caravan caravan = CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, e.originTile, true);
        foreach (Thing t in e.contents.ToList())
        {
            e.contents.Remove(t);
            CaravanInventoryUtility.GiveThing(caravan, t);
        }
        Find.LetterStack.ReceiveLetter("Exp_ReturnedLabel".Translate(e.opp.Label), "Exp_ReturnedAsCaravan".Translate(), LetterDefOf.NeutralEvent, caravan);
    }

    public static string ReportText(ExpeditionReport r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(("Exp_Outcome_" + r.outcome).Translate());
        if (r.outcome != Outcome.Failure && r.hasFinds) sb.AppendLine((r.found ? "Exp_Found" : "Exp_NotFound").Translate());
        if (!r.successText.NullOrEmpty()) sb.AppendLine(r.successText);
        foreach (string line in r.events) sb.AppendLine(line);
        sb.AppendLine();
        foreach (PawnResult p in r.pawns)
            sb.AppendLine($"{p.pawn.LabelShortCap}: {("Exp_Fate_" + p.fate).Translate()}" + (p.injuries.Count > 0 ? $" ({string.Join(", ", p.injuries)})" : ""));
        if (r.loot.Count > 0)
            sb.AppendLine().AppendLine("Exp_LootHeader".Translate() + " " + string.Join(", ", r.loot.Select(l => l.Label)));
        if (r.lostToHaul.Count > 0)
            sb.AppendLine("Exp_LostToHaulHeader".Translate() + " " + string.Join(", ", r.lostToHaul.Select(l => l.Label)));
        foreach (string line in GoodwillLines(r)) sb.AppendLine(line);
        foreach (string line in r.effectLines) sb.AppendLine(line);
        if (!r.relationsText.NullOrEmpty()) sb.AppendLine(r.relationsText);
        if (!r.retaliationText.NullOrEmpty()) sb.AppendLine(r.retaliationText);
        if (!r.siteFateText.NullOrEmpty()) sb.AppendLine(r.siteFateText);
        return sb.ToString().TrimEnd();
    }

    public static IEnumerable<string> GoodwillLines(ExpeditionReport r)
    {
        if (r.goodwill > 0 && r.goodwillFaction != null) yield return "Exp_GoodwillLine".Translate(r.goodwillFaction.Name, r.goodwill);
        if (r.goodwillAll > 0) yield return "Exp_GoodwillAllLine".Translate(r.goodwillAll);
    }
}
