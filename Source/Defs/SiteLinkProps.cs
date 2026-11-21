using RimWorld;
using Verse;

namespace Expeditions;

// Who answers a partial win at a linked site with a raid on the colony.
public enum RaidSource
{
    SiteFaction,
    Mechanoids,
    Insects,
    Pirates,
    Manhunters,
}

public class RetaliationProps
{
    public RaidSource source;
    public PawnKindDef? animalKind; // manhunters: this animal, else the home biome's own
    public float pointsFactor = 0.5f;
    public int delayTicks = 30000;
}

// A site def offered for a vanilla quest site on the world map: which quests it answers, and how it ends.
public class SiteLinkProps
{
    public Type workerClass = typeof(SiteLinkWorker);
    public List<QuestScriptDef> questRoots = new();
    public List<SitePartDef> siteParts = new(); // when set, the site must also have one of these parts

    public bool failureKeepsSite; // harmful sites: a failed squad leaves them where they are
    public bool affectRelations;  // the owner turns hostile, as when attacked in person
    public bool raidCredit;       // a win counts as a raid for Ideology

    public float fightPerThreatPoint = 1f;
    public float fightMin = 100f;

    public List<ThingDefCountClass> rewardCounts = new(); // counts for rewards the quest lists without one
    public List<ThingSetMakerDef> lootMakers = new(); // vanilla loot makers, each rolled lootMakerRolls times
    public int lootMakerRolls = 1;

    public RetaliationProps? retaliation;

    [Unsaved] private SiteLinkWorker? worker;

    public SiteLinkWorker Worker(ExpeditionSiteDef def)
    {
        if (worker == null)
        {
            worker = (SiteLinkWorker)Activator.CreateInstance(workerClass)!;
            worker.def = def;
        }
        return worker;
    }

    public void ResolveReferences()
    {
        questRoots.RemoveAll(q => q == null);
        siteParts.RemoveAll(p => p == null);
        rewardCounts.RemoveAll(r => r?.thingDef == null);
        lootMakers.RemoveAll(m => m == null);
    }

    public IEnumerable<string> ConfigErrors()
    {
        if (workerClass == null || !typeof(SiteLinkWorker).IsAssignableFrom(workerClass))
            yield return $"link.workerClass {workerClass} is not a SiteLinkWorker";
        if (questRoots.All(q => q == null)) yield return "link has no questRoots";
        if (lootMakerRolls < 0) yield return "link.lootMakerRolls must not be negative";
    }
}
