using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace Expeditions;

// Getting a squad off the map: checks, food pickups, walking out or boarding, and the cancel when the squad falls apart.
public static class Departure
{
    public static int Tiles(Map map, Opportunity opp) => (int)Math.Ceiling(Find.WorldGrid.ApproxDistanceInTiles(map.Tile, opp.tile));

    public static int People(IEnumerable<Pawn> squad) => squad.Count(p => p.RaceProps.Humanlike);

    // Days walking there for this squad, or -1 when there is no path.
    public static double FootDays(Map map, Opportunity opp, List<Pawn> squad)
    {
        // As vanilla caravans count it: only what they carry, not their own bodies, so light squads walk faster.
        float mass = squad.Sum(MassUtility.GearAndInventoryMass);
        float capacity = squad.Sum(p => MassUtility.Capacity(p, null));
        int ticks = TravelEstimator.FootTicks(map.Tile, opp.tile, squad, mass, Math.Max(capacity, 1f));
        return ticks < 0 ? -1 : ticks / (double)GenDate.TicksPerDay;
    }

    public static double TripDays(Map map, Opportunity opp, List<Pawn> squad, Ride ride)
    {
        double foot = FootDays(map, opp, squad);
        if (foot < 0 && ride != Ride.Shuttle) return -1;
        return TravelMath.TripDays(ride, Math.Max(foot, 0), Tiles(map, opp), opp.site.daysOnSite, ExpeditionsDefOf.Tuning.Travel);
    }

    public static AcceptanceReport CanLaunch(Opportunity opp, List<Pawn> squad, RideOption ride, Map map)
    {
        if (!map.listerBuildings.allBuildingsColonist.OfType<Building_CommsConsole>().Any(c => c.CanUseCommsNow))
            return "Exp_NeedConsole".Translate();
        int people = People(squad);
        if (people < opp.site.squadSize.min || people > opp.site.squadSize.max)
            return "Exp_SquadSize".Translate(opp.site.squadSize.min, opp.site.squadSize.max);
        var game = ExpeditionsGame.Instance;
        if (squad.Any(p => game.IsCommitted(p))) return "Exp_PawnBusy".Translate();
        if (squad.Any(p => p.Downed || p.InMentalState || !p.Spawned || p.Map != map)) return "Exp_PawnUnavailable".Translate();
        if (!ride.available) return ride.reason ?? "";
        double trip = TripDays(map, opp, squad, ride.ride);
        if (trip < 0) return "Exp_NoPath".Translate();
        foreach (Pawn p in squad)
        {
            AcceptanceReport can = Eligibility.CanGo(p, trip);
            if (!can.Accepted) return $"{p.LabelShortCap}: {can.Reason}";
        }
        AcceptanceReport squadOk = Eligibility.SquadOk(squad);
        if (!squadOk.Accepted) return squadOk;
        // A linked site leaves on its quest's timer; a squad that would get there after it is gone is not sent.
        if (opp.linkedSite != null && opp.expiresTick >= 0)
        {
            double outDays = TravelMath.OutDays(ride.ride, Math.Max(FootDays(map, opp, squad), 0), Tiles(map, opp), ExpeditionsDefOf.Tuning.Travel);
            if (Find.TickManager.TicksGame + outDays * GenDate.TicksPerDay >= opp.expiresTick) return "Exp_WorksiteTooFar".Translate();
        }
        float need = FoodPlanner.Required(squad, trip);
        float have = FoodPlanner.Available(squad, map, need, out _);
        if (FoodMath.Shortfall(need, have) > 0.001) return "Exp_NotEnoughFood".Translate(need.ToString("F1"), have.ToString("F1"));
        return true;
    }

    public static Expedition Launch(Opportunity opp, List<Pawn> squad, RideOption ride, Map map)
    {
        var game = ExpeditionsGame.Instance;
        double foot = Math.Max(FootDays(map, opp, squad), 0);
        double trip = TravelMath.TripDays(ride.ride, foot, Tiles(map, opp), opp.site.daysOnSite, ExpeditionsDefOf.Tuning.Travel);
        float need = FoodPlanner.Required(squad, trip);
        FoodPlanner.Available(squad, map, need, out var pickups);

        var e = new Expedition
        {
            id = game.NextId(),
            opp = opp,
            ride = ride.ride,
            originMap = map,
            originTile = map.Tile,
            tripFootDays = foot,
            foodNeeded = need,
            squadMin = opp.site.squadSize.min,
        };
        e.squad.AddRange(squad);
        e.hirees.AddRange(squad.Where(Eligibility.IsHiree));
        e.rideThings.AddRange(ride.things);
        e.StartPhase(Phase.Departing, GenDate.TicksPerDay);
        game.offers.Remove(opp);
        game.active.Add(e);

        var carriers = squad.Where(p => p.RaceProps.Humanlike).ToList();
        for (int i = 0; i < pickups.Count; i++)
        {
            Pawn carrier = carriers[i % carriers.Count];
            Job take = JobMaker.MakeJob(JobDefOf.TakeInventory, pickups[i].thing);
            take.count = pickups[i].count;
            Order(carrier, take, queue: true);
        }
        // A member who cannot reach the edge or the ride gets no order and drops out on the next check.
        foreach (Pawn p in squad)
            if (DepartJob(e, p) is { } depart) Order(p, depart, queue: true);
        return e;
    }

    // Null when the pawn has no way out: no reachable ride, no reachable map edge.
    public static Job? DepartJob(Expedition e, Pawn pawn)
    {
        Job job;
        if (e.rideThings.FirstOrDefault(t => t.Spawned) is { } ride)
        {
            if (!pawn.CanReach(ride, PathEndMode.Touch, Danger.Deadly)) return null;
            job = JobMaker.MakeJob(ExpeditionsDefOf.Exp_Depart, ride);
        }
        else
        {
            if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 spot, TraverseMode.ByPawn, true) || !spot.IsValid) return null;
            job = JobMaker.MakeJob(ExpeditionsDefOf.Exp_Depart, spot);
        }
        job.count = e.id;
        return job;
    }

    // Called by JobDriver_ExpeditionDepart when a squad member reaches the edge or the ride.
    public static void Board(Pawn pawn, int expeditionId)
    {
        Expedition? e = ExpeditionsGame.Instance.ById(expeditionId);
        if (e == null || e.phase != Phase.Departing || !e.squad.Contains(pawn)) return;
        e.Take(pawn);
    }

    public static void Tick(Expedition e)
    {
        Map? map = e.originMap;
        if (map == null || !Find.Maps.Contains(map))
        {
            Cancel(e, null);
            return;
        }
        // Departure has a deadline (a day from launch): whoever has not boarded by then stays behind.
        bool late = Find.TickManager.TicksGame > e.phaseEndTick;
        e.squad.RemoveAll(p => !e.contents.Contains(p) && (late || DroppedOut(p, map, e)));
        if (People(e.squad) < e.squadMin)
        {
            Cancel(e, map);
            return;
        }
        if (!e.squad.All(p => e.contents.Contains(p))) return;

        int tiles = Tiles(map, e.opp);
        foreach (Thing t in e.rideThings.ToList())
        {
            if (e.ride == Ride.DropPods)
            {
                var launchable = t.TryGetComp<CompLaunchable>();
                launchable?.Refuelable?.ConsumeFuel(launchable.Props.fuelPerTile * tiles);
                if (t.Spawned) FleckMaker.ThrowSmoke(t.DrawPos, map, 2f);
                t.Destroy(DestroyMode.Vanish);
            }
            else if (t.Spawned)
            {
                if (e.ride == Ride.Shuttle)
                {
                    var launchable = t.TryGetComp<CompLaunchable>();
                    launchable?.Refuelable?.ConsumeFuel(launchable.Props.fuelPerTile * tiles * 2);
                }
                e.Take(t);
            }
        }
        e.rideThings.RemoveAll(t => t.Destroyed);
        double outDays = TravelMath.OutDays(e.ride, e.tripFootDays, tiles, ExpeditionsDefOf.Tuning.Travel);
        e.StartPhase(Phase.Outbound, (int)(outDays * GenDate.TicksPerDay));
        Messages.Message("Exp_Departed".Translate(e.opp.Label), MessageTypeDefOf.NeutralEvent, false);
    }

    // A squad member who can no longer leave: downed, dead, broken, drafted, off the map, or sent elsewhere.
    private static bool DroppedOut(Pawn p, Map map, Expedition e)
    {
        if (p.Dead || p.Downed || p.InMentalState || !p.Spawned || p.Map != map) return true;
        if (p.Drafted) return true;
        bool ours(Job? j) => j != null && (j.def == ExpeditionsDefOf.Exp_Depart || j.def == JobDefOf.TakeInventory);
        if (ours(p.CurJob) || p.jobs.jobQueue.Any(q => ours(q.job))) return false;
        // Animals' think trees may drop the order; give it again rather than losing the animal, unless it has no way out.
        if (!p.RaceProps.Humanlike && DepartJob(e, p) is { } again)
        {
            Order(p, again, queue: false);
            return false;
        }
        return true;
    }

    // The player calls it off from the Active tab.
    public static void CallOff(Expedition e, string messageKey = "Exp_DepartureCancelled") => Cancel(e, e.originMap, messageKey);

    // Nothing is lost: boarded pawns come back where they left (or to another home map, or as a caravan when there
    // is no home left), food stays in their pockets, the offer returns.
    private static void Cancel(Expedition e, Map? map, string messageKey = "Exp_DepartureCancelled")
    {
        var game = ExpeditionsGame.Instance;
        Map? target = map != null && Find.Maps.Contains(map) ? map : Find.AnyPlayerHomeMap;
        if (target != null)
        {
            IntVec3 near = e.squad.FirstOrDefault(p => p.Spawned && p.Map == target)?.Position ?? DropCellFinder.TradeDropSpot(target);
            foreach (Thing t in e.contents.ToList()) e.Release(t, target, near);
        }
        else if (e.AwayPawns.Any())
        {
            Arrival.BecomeCaravan(e, e.AwayPawns.Where(p => !p.Dead).ToList());
        }
        foreach (Pawn p in e.squad.Where(p => p.Spawned && p.CurJobDef == ExpeditionsDefOf.Exp_Depart)) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        game.active.Remove(e);
        if (e.opp.expiresTick < 0 || e.opp.expiresTick > Find.TickManager.TicksGame) game.offers.Add(e.opp);
        Messages.Message(messageKey.Translate(e.opp.Label), MessageTypeDefOf.NegativeEvent, false);
    }

    private static void Order(Pawn p, Job job, bool queue)
    {
        if (p.drafter != null) p.drafter.Drafted = false;
        if (p.RaceProps.Humanlike) p.jobs.TryTakeOrderedJob(job, JobTag.Misc, queue);
        else p.jobs.StartJob(job, JobCondition.InterruptForced);
    }
}
