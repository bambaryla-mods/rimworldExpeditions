// Logic: no Verse, RimWorld or UnityEngine here. Source/Tests compiles this folder without game references.
namespace Expeditions;

// How a squad's per-pawn scores become one score for a challenge.
public enum CombineRule
{
    Sum,     // more pawns help (fight, haul)
    Min,     // weakest pawn decides (stealth)
    Max,     // one expert is enough (search)
    Average,
}

public enum Outcome
{
    Success,
    Partial,
    Failure,
}

public enum PawnFate
{
    Unharmed,
    Injured,
    Killed,
    Captured,
}

public enum Ride
{
    Foot,
    DropPods,
    Shuttle,
    Vehicles,
}

public enum MapStyle
{
    Vanilla,
    Holo,
}

public enum Phase
{
    Departing,
    Outbound,
    OnSite,
    Returning,
}
