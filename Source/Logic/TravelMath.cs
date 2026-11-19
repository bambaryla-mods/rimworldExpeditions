// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public readonly record struct TravelTuning(double FlightBaseDays = 0.25, double FlightDaysPerTile = 0.01, double VehicleFootFactor = 0.5)
{
    public TravelTuning() : this(0.25, 0.01, 0.5) { }
}

public static class TravelMath
{
    public static double OutDays(Ride ride, double footDays, double tiles, TravelTuning t) => ride switch
    {
        Ride.Foot => footDays,
        Ride.DropPods or Ride.Shuttle => Flight(tiles, t),
        Ride.Vehicles => footDays * t.VehicleFootFactor,
        _ => throw new ArgumentOutOfRangeException(nameof(ride)),
    };

    // Pods are one-way, so a pod squad walks home.
    public static double BackDays(Ride ride, double footDays, double tiles, TravelTuning t) => ride switch
    {
        Ride.Foot or Ride.DropPods => footDays,
        Ride.Shuttle => Flight(tiles, t),
        Ride.Vehicles => footDays * t.VehicleFootFactor,
        _ => throw new ArgumentOutOfRangeException(nameof(ride)),
    };

    public static double TripDays(Ride ride, double footDays, double tiles, double siteDays, TravelTuning t) =>
        OutDays(ride, footDays, tiles, t) + siteDays + BackDays(ride, footDays, tiles, t);

    public static bool IsLoud(Ride ride) => ride != Ride.Foot;

    private static double Flight(double tiles, TravelTuning t) => t.FlightBaseDays + t.FlightDaysPerTile * tiles;
}
