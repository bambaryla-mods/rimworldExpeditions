using RimWorld;
using Verse;

namespace Expeditions;

// All expedition state for one game: offers on the board, squads out, reports, prospecting progress, captives.
public class ExpeditionsGame : GameComponent, IThingHolder
{
    public const int HistoryCap = 20;
    public const int TickInterval = 250;

    public static ExpeditionsGame Instance = null!;

    public List<Opportunity> offers = new();
    public List<Expedition> active = new();
    public List<ExpeditionReport> history = new();

    public Dictionary<ThingDef, int> prospectTicksDone = new();
    public HashSet<ThingDef> prospectedThisWindow = new();
    public int prospectWindow = -1;

    public Dictionary<Pawn, int> capturedTick = new();
    public int nextOfferTick;
    private int nextId = 1;

    private List<ThingDef>? prospectKeys;
    private List<int>? prospectValues;
    private List<Pawn>? capturedKeys;
    private List<int>? capturedValues;

    public ExpeditionsGame(Game game)
    {
        Instance = this;
        WorldArea.ClearCache();
        TravelEstimator.ClearCache();
    }

    public int NextId() => nextId++;

    public IEnumerable<Pawn> AllAwayPawns => active.SelectMany(e => e.AwayPawns);

    public bool IsAway(Pawn p) => active.Any(e => e.contents.Contains(p));

    // In any expedition's squad, including one still walking out.
    public bool IsCommitted(Pawn p) => active.Any(e => e.squad.Contains(p)) || IsAway(p);

    public Expedition? ById(int id) => active.FirstOrDefault(e => e.id == id);

    public void HolderChanged() => Find.ColonistBar?.MarkColonistsDirty();

    public void AddHistory(ExpeditionReport report)
    {
        history.Insert(0, report);
        if (history.Count > HistoryCap) history.RemoveRange(HistoryCap, history.Count - HistoryCap);
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval == 0) Tick250();
    }

    // Phase flow: Departing (until everyone has boarded) -> Outbound -> OnSite (then resolve) -> Returning (then home).
    public void Tick250()
    {
        int now = Find.TickManager.TicksGame;
        foreach (Expedition e in active.ToList())
        {
            switch (e.phase)
            {
                case Phase.Departing:
                    Departure.Tick(e);
                    break;
                case Phase.Outbound when now >= e.phaseEndTick:
                    e.StartPhase(Phase.OnSite, (int)(e.opp.site.daysOnSite * GenDate.TicksPerDay));
                    break;
                case Phase.OnSite when now >= e.phaseEndTick:
                    Resolver.Resolve(e);
                    break;
                case Phase.Returning when now >= e.phaseEndTick:
                    Arrival.Return(e);
                    break;
            }
        }
        OfferGenerator.Tick();
    }

    public IThingHolder? ParentHolder => null;

    public void GetChildHolders(List<IThingHolder> outChildren) => outChildren.AddRange(active);

    public ThingOwner? GetDirectlyHeldThings() => null;

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref offers, "offers", LookMode.Deep);
        Scribe_Collections.Look(ref active, "active", LookMode.Deep);
        Scribe_Collections.Look(ref history, "history", LookMode.Deep);
        Scribe_Collections.Look(ref prospectTicksDone, "prospectTicksDone", LookMode.Def, LookMode.Value, ref prospectKeys, ref prospectValues);
        Scribe_Collections.Look(ref prospectedThisWindow, "prospectedThisWindow", LookMode.Def);
        Scribe_Values.Look(ref prospectWindow, "prospectWindow", -1);
        Scribe_Collections.Look(ref capturedTick, "capturedTick", LookMode.Reference, LookMode.Value, ref capturedKeys, ref capturedValues);
        Scribe_Values.Look(ref nextOfferTick, "nextOfferTick");
        Scribe_Values.Look(ref nextId, "nextId", 1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            offers ??= new();
            active ??= new();
            history ??= new();
            prospectTicksDone ??= new();
            prospectedThisWindow ??= new();
            capturedTick ??= new();
            offers.RemoveAll(o => o?.site == null);
            active.RemoveAll(e => e?.opp?.site == null);
            prospectTicksDone.RemoveAll(kv => kv.Key == null);
            prospectedThisWindow.RemoveWhere(d => d == null);
            capturedTick.RemoveAll(kv => kv.Key == null);
        }
    }
}
