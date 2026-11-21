using RimWorld;
using Verse;

namespace Expeditions;

public class PawnResult : IExposable
{
    public Pawn pawn = null!;
    public PawnFate fate;
    public List<string> injuries = new();
    public Dictionary<SkillDef, float> xp = new();

    private List<SkillDef>? xpKeys;
    private List<float>? xpValues;

    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn", saveDestroyedThings: true);
        Scribe_Values.Look(ref fate, "fate");
        Scribe_Collections.Look(ref injuries, "injuries", LookMode.Value);
        Scribe_Collections.Look(ref xp, "xp", LookMode.Def, LookMode.Value, ref xpKeys, ref xpValues);
        injuries ??= new();
        xp ??= new();
    }
}

// What happened, written when the squad resolves the site and shown when they get home.
public class ExpeditionReport : IExposable
{
    public string siteLabel = "";
    public Outcome outcome;
    public bool found;
    public int tick;
    public List<PawnResult> pawns = new();
    public List<ThingDefCountClass> loot = new();
    public List<ThingDefCountClass> lostToHaul = new();

    // Goodwill earned: from the patron, and from each friendly faction.
    public Faction? goodwillFaction;
    public int goodwill;
    public int goodwillAll;

    public bool hasFinds = true;  // false for jobs with nothing to find: no "found nothing" line
    public string? successText;
    public string? relationsText; // the site owner's change of heart after a work site raid
    public string? retaliationText; // who will raid the colony for a partial win
    public string? siteFateText;    // what became of a linked site the squad failed at

    public void ExposeData()
    {
        Scribe_Values.Look(ref siteLabel, "siteLabel", "");
        Scribe_Values.Look(ref outcome, "outcome");
        Scribe_Values.Look(ref found, "found");
        Scribe_Values.Look(ref tick, "tick");
        Scribe_Collections.Look(ref pawns, "pawns", LookMode.Deep);
        Scribe_Collections.Look(ref loot, "loot", LookMode.Deep);
        Scribe_Collections.Look(ref lostToHaul, "lostToHaul", LookMode.Deep);
        Scribe_References.Look(ref goodwillFaction, "goodwillFaction");
        Scribe_Values.Look(ref goodwill, "goodwill");
        Scribe_Values.Look(ref goodwillAll, "goodwillAll");
        Scribe_Values.Look(ref hasFinds, "hasFinds", true);
        Scribe_Values.Look(ref successText, "successText");
        Scribe_Values.Look(ref relationsText, "relationsText");
        Scribe_Values.Look(ref retaliationText, "retaliationText");
        Scribe_Values.Look(ref siteFateText, "siteFateText");
        pawns ??= new();
        loot ??= new();
        lostToHaul ??= new();
        if (Scribe.mode == LoadSaveMode.PostLoadInit) pawns.RemoveAll(p => p.pawn == null);
    }
}
