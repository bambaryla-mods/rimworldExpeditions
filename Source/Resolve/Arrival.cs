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
        if (!pawns.Any(p => p.RaceProps.Humanlike && !RideFinder.VehicleCompat.IsVehicle(p)))
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
            Find.LetterStack.ReceiveLetter("Exp_LostLabel".Translate(), "Exp_LostText".Translate(report.siteLabel), LetterDefOf.NegativeEvent);
            game.HolderChanged();
            return;
        }

        FoodPlanner.ConsumeOnReturn(pawns.Where(p => !RideFinder.VehicleCompat.IsVehicle(p)).ToList(), e.foodNeeded);

        Map? map = e.originMap != null && Find.Maps.Contains(e.originMap) && e.originMap.IsPlayerHome ? e.originMap : Find.AnyPlayerHomeMap;
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
                GenSpawn.Spawn(t, cell, map, Rot4.North);
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
        if (e.ride == Ride.Shuttle)
        {
            Thing? console = map.listerBuildings.allBuildingsColonist.OfType<Building_CommsConsole>().FirstOrDefault();
            IntVec3 near = console?.Position ?? DropCellFinder.TradeDropSpot(map);
            if (DropCellFinder.TryFindDropSpotNear(near, map, out IntVec3 spot, false, false, false, new IntVec2(5, 5))) return spot;
            return DropCellFinder.TradeDropSpot(map);
        }
        if (CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c),
                map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 edge))
            return edge;
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
        if (r.outcome != Outcome.Failure) sb.AppendLine((r.found ? "Exp_Found" : "Exp_NotFound").Translate());
        sb.AppendLine();
        foreach (PawnResult p in r.pawns)
            sb.AppendLine($"{p.pawn.LabelShortCap}: {("Exp_Fate_" + p.fate).Translate()}" + (p.injuries.Count > 0 ? $" ({string.Join(", ", p.injuries)})" : ""));
        if (r.loot.Count > 0)
            sb.AppendLine().AppendLine("Exp_LootHeader".Translate() + " " + string.Join(", ", r.loot.Select(l => l.Label)));
        if (r.lostToHaul.Count > 0)
            sb.AppendLine("Exp_LostToHaulHeader".Translate() + " " + string.Join(", ", r.lostToHaul.Select(l => l.Label)));
        return sb.ToString().TrimEnd();
    }
}
