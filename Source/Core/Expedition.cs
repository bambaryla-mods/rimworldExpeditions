using RimWorld.Planet;
using Verse;

namespace Expeditions;

// A squad out on one opportunity. While away, its pawns, animals, vehicles, shuttle and cargo sit in `contents`,
// which nothing ticks: they are frozen until they come home.
public class Expedition : IExposable, IThingHolder
{
    public int id;
    public Opportunity opp = null!;
    public Ride ride;
    public Phase phase;

    public List<Pawn> squad = new();
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
        if (thing is Pawn pawn) pawn.jobs?.StopAll();
        if (thing.Spawned) thing.DeSpawnOrDeselect();
        if (!contents.TryAddOrTransfer(thing, canMergeWithExistingStacks: false))
            Log.Error($"[Expeditions] Could not take {thing} into expedition {id}.");
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
        Scribe_Collections.Look(ref rideThings, "rideThings", LookMode.Reference);
        Scribe_References.Look(ref originMap, "originMap");
        Scribe_Values.Look(ref originTile, "originTile", PlanetTile.Invalid);
        Scribe_Values.Look(ref phaseEndTick, "phaseEndTick");
        Scribe_Values.Look(ref phaseStartTick, "phaseStartTick");
        Scribe_Values.Look(ref tripFootDays, "tripFootDays");
        Scribe_Values.Look(ref foodNeeded, "foodNeeded");
        Scribe_Values.Look(ref squadMin, "squadMin", 1);
        Scribe_Deep.Look(ref report, "report");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            contents ??= new ThingOwner<Thing>(this);
            squad ??= new();
            rideThings ??= new();
            squad.RemoveAll(p => p == null);
            rideThings.RemoveAll(t => t == null);
        }
    }

    private static float Mathf01(float f) => f < 0 ? 0 : f > 1 ? 1 : f;
}
