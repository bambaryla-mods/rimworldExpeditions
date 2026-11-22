using RimWorld.Planet;
using Verse;

namespace Expeditions;

// A squad out on one opportunity. While away, its pawns, animals, vehicles, shuttle and cargo sit in `contents`,
// which nothing ticks: they are frozen until they come home.
public class Expedition : IExposable, ISuspendableThingHolder
{
    // Frozen while away: vanilla treats suspended pawns as not needing food, warmth or mood alerts.
    public bool IsContentsSuspended => true;

    public int id;
    public Opportunity opp = null!;
    public Ride ride;
    public Phase phase;

    public List<Pawn> squad = new();
    public List<Pawn> hirees = new(); // Comm Recruiter hirees in the squad at launch
    public List<Thing> rideThings = new();
    public ThingOwner<Thing> contents;

    public Map? originMap;
    public PlanetTile originTile = PlanetTile.Invalid;

    public int phaseEndTick;
    public int phaseStartTick;
    public double tripFootDays;
    public float foodNeeded;
    public int squadMin = 1;

    public ExpeditionReport? report;
    public PendingRaid? retaliation; // owed for a partial win; handed to the game when the squad is home

    public Expedition()
    {
        contents = new ThingOwner<Thing>(this);
    }

    public IEnumerable<Pawn> AwayPawns => contents.OfType<Pawn>();

    public float PhaseProgress => phaseEndTick <= phaseStartTick
        ? 0f
        : Mathf01((Find.TickManager.TicksGame - phaseStartTick) / (float)(phaseEndTick - phaseStartTick));

    public void StartPhase(Phase next, int durationTicks)
    {
        phase = next;
        phaseStartTick = Find.TickManager.TicksGame;
        phaseEndTick = phaseStartTick + Math.Max(1, durationTicks);
    }

    // Moves a spawned thing into the holder.
    public void Take(Thing thing)
    {
        if (thing.Spawned) thing.DeSpawnOrDeselect();
        if (!contents.TryAddOrTransfer(thing, canMergeWithExistingStacks: false))
            Log.Error($"[Expeditions] Could not take {thing} into expedition {id}.");
        ExpeditionsGame.Instance.HolderChanged();
    }

    // A squad member leaves the expedition for good (dead, taken, fled, pulled away). Every such removal goes
    // through here, so a squad member missing from the holder always means someone else took them.
    public void Forget(Pawn p)
    {
        if (contents.Contains(p)) contents.Remove(p);
        squad.Remove(p);
        hirees.Remove(p);
        ExpeditionsGame.Instance.HolderChanged();
    }

    // Spawns a held thing back onto a map near a cell.
    public void Release(Thing thing, Map map, IntVec3 near)
    {
        contents.Remove(thing);
        if (thing is Pawn pawn)
        {
            IntVec3 cell = CellFinder.StandableCellNear(near, map, 6f);
            GenSpawn.Spawn(pawn, cell.IsValid ? cell : near, map);
        }
        else
        {
            GenPlace.TryPlaceThing(thing, near, map, ThingPlaceMode.Near);
        }
        ExpeditionsGame.Instance.HolderChanged();
    }

    public IThingHolder? ParentHolder => ExpeditionsGame.Instance;

    public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, contents);

    public ThingOwner GetDirectlyHeldThings() => contents;

    public void ExposeData()
    {
        Scribe_Values.Look(ref id, "id");
        Scribe_Deep.Look(ref opp, "opp");
        Scribe_Values.Look(ref ride, "ride");
        Scribe_Values.Look(ref phase, "phase");
        Scribe_Deep.Look(ref contents, "contents", this);
        Scribe_Collections.Look(ref squad, "squad", LookMode.Reference);
        Scribe_Collections.Look(ref hirees, "hirees", LookMode.Reference);
        Scribe_Collections.Look(ref rideThings, "rideThings", LookMode.Reference);
        Scribe_References.Look(ref originMap, "originMap");
        Scribe_Values.Look(ref originTile, "originTile", PlanetTile.Invalid);
        Scribe_Values.Look(ref phaseEndTick, "phaseEndTick");
        Scribe_Values.Look(ref phaseStartTick, "phaseStartTick");
        Scribe_Values.Look(ref tripFootDays, "tripFootDays");
        Scribe_Values.Look(ref foodNeeded, "foodNeeded");
        Scribe_Values.Look(ref squadMin, "squadMin", 1);
        Scribe_Deep.Look(ref report, "report");
        Scribe_Deep.Look(ref retaliation, "retaliation");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            contents ??= new ThingOwner<Thing>(this);
            squad ??= new();
            hirees ??= new();
            hirees.RemoveAll(p => p == null);
            rideThings ??= new();
            squad.RemoveAll(p => p == null);
            rideThings.RemoveAll(t => t == null);
        }
    }

    private static float Mathf01(float f) => f < 0 ? 0 : f > 1 ? 1 : f;
}
