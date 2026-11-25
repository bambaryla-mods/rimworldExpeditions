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
    Gone, // never rolled: the site was gone by the time the squad got there
    CutShort, // never rolled: something on the road (a betrayal, a turned creepjoiner) sent the squad home early
}

public enum PawnFate
{
    Unharmed,
    Injured,
    Killed,
    Captured,
    Joined, // never rolled: someone who came home with the squad and joined the colony
    RanAway, // never rolled: a slave who fled, or a creepjoiner who walked off or turned
    Taken, // never rolled: someone the squad brought home against their will
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
