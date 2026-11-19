using HarmonyLib;
using RimWorld;
using Verse;

namespace Expeditions;

// Which rides a squad can take from a map right now, and what each brings.
public static class RideFinder
{
    public static float SquadMass(IEnumerable<Pawn> squad) =>
        squad.Sum(p => p.GetStatValue(StatDefOf.Mass) + MassUtility.GearAndInventoryMass(p));

    public static List<RideOption> For(Map map, List<Pawn> squad, int tiles)
    {
        float mass = SquadMass(squad);
        return new List<RideOption> { Foot(), Pods(map, mass, tiles), Shuttle(map, mass, tiles), Vehicles(map, squad) }
            .Where(o => o.ride != Ride.Vehicles || VehicleCompat.Loaded)
            .ToList();
    }

    // The ride an away squad holds, rebuilt for scoring at the site.
    public static RideOption Held(Expedition e) =>
        new(e.ride, true, null, HeldCargo(e), e.contents.OfType<Pawn>().Count(p => VehicleCompat.IsVehicle(p) && VehicleCompat.Armed(p)),
            e.contents.Where(t => t is not Pawn || VehicleCompat.IsVehicle((Pawn)t)).ToList());

    // Extra cargo room from a held shuttle or vehicles, beyond what the pawns carry.
    public static float HeldCargo(Expedition e)
    {
        var people = e.contents.OfType<Pawn>().Where(p => !VehicleCompat.IsVehicle(p)).ToList();
        if (e.ride == Ride.Shuttle)
        {
            float capacity = e.contents.Select(t => t.TryGetComp<CompTransporter>()).Where(c => c != null).Sum(c => c!.MassCapacity);
            return Math.Max(0f, capacity - SquadMass(people));
        }
        if (e.ride == Ride.Vehicles) return e.contents.OfType<Pawn>().Where(VehicleCompat.IsVehicle).Sum(VehicleCompat.Cargo);
        return 0f;
    }

    private static RideOption Foot() => new(Ride.Foot, true, null, 0f, 0, new List<Thing>());

    // Drop pods: transporters that launch and are not shuttles. They are used up on launch.
    private static RideOption Pods(Map map, float mass, int tiles)
    {
        var pods = map.listerThings.AllThings
            .Where(t => t.Faction == Faction.OfPlayer && t.TryGetComp<CompLaunchable>() != null && t.TryGetComp<CompShuttle>() == null && t.TryGetComp<CompTransporter>() != null)
            .Where(t => t.TryGetComp<CompLaunchable>()!.MaxLaunchDistanceAtFuelLevel(t.TryGetComp<CompLaunchable>()!.FuelLevel) >= tiles)
            .ToList();
        if (pods.Count == 0) return new(Ride.DropPods, false, "Exp_NoPods".Translate(), 0f, 0, new List<Thing>());
        var chosen = new List<Thing>();
        float capacity = 0f;
        foreach (Thing pod in pods)
        {
            if (capacity >= mass) break;
            chosen.Add(pod);
            capacity += pod.TryGetComp<CompTransporter>()!.MassCapacity;
        }
        return capacity < mass
            ? new(Ride.DropPods, false, "Exp_PodsTooSmall".Translate(capacity.ToString("F0"), mass.ToString("F0")), 0f, 0, chosen)
            : new(Ride.DropPods, true, null, 0f, 0, chosen);
    }

    // A shuttle flies out and back, so its fuel must cover both legs. Odyssey's passenger shuttle and Royalty's
    // shuttles are both a thing with CompShuttle and CompLaunchable.
    private static RideOption Shuttle(Map map, float mass, int tiles)
    {
        Thing? shuttle = map.listerThings.AllThings.FirstOrDefault(t =>
            t.Faction == Faction.OfPlayer && t.TryGetComp<CompLaunchable>() != null && t.TryGetComp<CompShuttle>() != null);
        if (shuttle == null) return new(Ride.Shuttle, false, "Exp_NoShuttle".Translate(), 0f, 0, new List<Thing>());
        var launchable = shuttle.TryGetComp<CompLaunchable>()!;
        if (launchable.MaxLaunchDistanceAtFuelLevel(launchable.FuelLevel) < tiles * 2)
            return new(Ride.Shuttle, false, "Exp_ShuttleFuel".Translate(), 0f, 0, new List<Thing> { shuttle });
        float capacity = shuttle.TryGetComp<CompTransporter>()?.MassCapacity ?? 0f;
        if (capacity < mass)
            return new(Ride.Shuttle, false, "Exp_ShuttleTooSmall".Translate(), 0f, 0, new List<Thing> { shuttle });
        return new(Ride.Shuttle, true, null, capacity - mass, 0, new List<Thing> { shuttle });
    }

    private static RideOption Vehicles(Map map, List<Pawn> squad)
    {
        var vehicles = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer && VehicleCompat.IsVehicle(p)).ToList();
        int people = squad.Count(p => p.RaceProps.Humanlike);
        if (vehicles.Count == 0) return new(Ride.Vehicles, false, "Exp_NoVehicles".Translate(), 0f, 0, new List<Thing>());
        var chosen = new List<Thing>();
        int seats = 0;
        foreach (Pawn v in vehicles.OrderByDescending(VehicleCompat.Seats))
        {
            if (seats >= people) break;
            chosen.Add(v);
            seats += VehicleCompat.Seats(v);
        }
        if (seats < people) return new(Ride.Vehicles, false, "Exp_NotEnoughSeats".Translate(seats, people), 0f, 0, chosen);
        return new(Ride.Vehicles, true, null, chosen.Sum(v => VehicleCompat.Cargo((Pawn)v)), chosen.Count(v => VehicleCompat.Armed((Pawn)v)), chosen);
    }

    // Vehicle Framework, by reflection only. Member names checked against VF's 1.7 Vehicles.dll. If anything is
    // missing, the Vehicles ride is hidden and one warning is logged.
    public static class VehicleCompat
    {
        private static bool resolved;
        private static bool ok;
        private static Type? vehicleType;
        private static Func<object, int>? totalSeats;
        private static Func<object, object?>? turrets;
        private static Func<object, float>? cargo;

        public static bool Loaded
        {
            get
            {
                if (!resolved) Resolve();
                return ok;
            }
        }

        public static bool IsVehicle(Pawn p) => Loaded && vehicleType!.IsInstanceOfType(p);
        public static int Seats(Pawn p) => Loaded ? totalSeats!(p) : 0;
        public static bool Armed(Pawn p) => Loaded && turrets!(p) != null;
        public static float Cargo(Pawn p) => Loaded ? cargo!(p) : 0f;

        private static void Resolve()
        {
            resolved = true;
            vehicleType = GenTypes.GetTypeInAnyAssembly("Vehicles.VehiclePawn");
            if (vehicleType == null) return;
            try
            {
                var seatsProp = AccessTools.Property(vehicleType, "TotalSeats") ?? throw new MissingMemberException("VehiclePawn.TotalSeats");
                var turretsProp = AccessTools.Property(vehicleType, "CompVehicleTurrets") ?? throw new MissingMemberException("VehiclePawn.CompVehicleTurrets");
                Type statDefType = GenTypes.GetTypeInAnyAssembly("Vehicles.VehicleStatDef") ?? throw new MissingMemberException("Vehicles.VehicleStatDef");
                var getStat = AccessTools.Method(vehicleType, "GetStatValue", new[] { statDefType }) ?? throw new MissingMemberException("VehiclePawn.GetStatValue");
                Type defOf = GenTypes.GetTypeInAnyAssembly("Vehicles.VehicleStatDefOf") ?? throw new MissingMemberException("Vehicles.VehicleStatDefOf");
                object cargoStat = AccessTools.Field(defOf, "CargoCapacity")?.GetValue(null) ?? throw new MissingMemberException("VehicleStatDefOf.CargoCapacity");
                totalSeats = v => (int)seatsProp.GetValue(v)!;
                turrets = v => turretsProp.GetValue(v);
                cargo = v => (float)getStat.Invoke(v, new[] { cargoStat })!;
                ok = true;
            }
            catch (Exception e)
            {
                Log.Warning("[Expeditions] Vehicle Framework is loaded but its API does not match; the Vehicles ride is off. " + e.Message);
            }
        }
    }
}
