using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// A raid owed to the colony for a partial win at a linked site. Kept by the mod (not vanilla's incident queue, which
// drops an incident after one failed try and holds a map that may be gone), saved, and fired at a home map.
public class PendingRaid : IExposable
{
    public RaidSource source;
    public Faction? faction; // SiteFaction only: a real, lasting faction
    public PawnKindDef? animal;
    public float pointsFactor = 0.5f;
    public int delayTicks = 30000;
    public int fireTick = -1;
    public Map? map;
    public int retries;

    public void ExposeData()
    {
        Scribe_Values.Look(ref source, "source");
        Scribe_References.Look(ref faction, "faction");
        Scribe_Defs.Look(ref animal, "animal");
        Scribe_Values.Look(ref pointsFactor, "pointsFactor", 0.5f);
        Scribe_Values.Look(ref delayTicks, "delayTicks", 30000);
        Scribe_Values.Look(ref fireTick, "fireTick", -1);
        Scribe_References.Look(ref map, "map");
        Scribe_Values.Look(ref retries, "retries");
    }
}

public static class Retaliation
{
    private const int RetryTicks = GenDate.TicksPerDay;
    private const int MaxRetries = 3;
    private const float NonRaidFloor = 200f; // infestations and manhunters have no group minimum of their own

    // Who will answer a partial win at this expedition's site, read before the site is gone.
    public static PendingRaid? For(Expedition e)
    {
        RetaliationProps? r = e.opp.site.link?.retaliation;
        if (r == null) return null;
        var raid = new PendingRaid { source = r.source, animal = r.animalKind, pointsFactor = r.pointsFactor, delayTicks = r.delayTicks };
        if (raid.source == RaidSource.SiteFaction)
        {
            Faction? owner = e.opp.linkedSite?.Faction ?? e.opp.faction;
            if (owner != null && owner == Faction.OfMechanoids) raid.source = RaidSource.Mechanoids;
            else if (owner != null && owner == Faction.OfInsects) raid.source = RaidSource.Insects;
            else if (owner is { Hidden: false, temporary: false, defeated: false } f && !f.IsPlayer) raid.faction = f;
            else raid.source = RaidSource.Pirates;
        }
        return raid;
    }

    // Who the report names as coming.
    public static string Who(PendingRaid raid) => raid.source switch
    {
        RaidSource.SiteFaction when raid.faction != null => raid.faction.Name,
        RaidSource.Mechanoids => "Exp_RaidMechs".Translate(),
        RaidSource.Insects => "Exp_RaidInsects".Translate(),
        RaidSource.Manhunters => "Exp_RaidAnimals".Translate(),
        _ => "Exp_RaidPirates".Translate(),
    };

    // The squad is home (or a caravan): the raid starts counting down.
    public static void Hand(Expedition e, Map? home)
    {
        if (e.retaliation is not { } raid) return;
        e.retaliation = null;
        raid.map = home;
        raid.fireTick = Find.TickManager.TicksGame + raid.delayTicks;
        ExpeditionsGame.Instance.pendingRaids.Add(raid);
    }

    public static void Tick()
    {
        var raids = ExpeditionsGame.Instance.pendingRaids;
        int now = Find.TickManager.TicksGame;
        foreach (PendingRaid raid in raids.Where(r => r.fireTick <= now).ToList())
        {
            if (Fire(raid))
            {
                raids.Remove(raid);
                continue;
            }
            if (++raid.retries > MaxRetries) raids.Remove(raid);
            else raid.fireTick = now + RetryTicks;
        }
    }

    private static bool Fire(PendingRaid raid)
    {
        Map? map = raid.map != null && Find.Maps.Contains(raid.map) && raid.map.IsPlayerHome ? raid.map : Find.AnyPlayerHomeMap;
        if (map == null) return false;
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.forced = true;
        float now = StorytellerUtility.DefaultThreatPointsNow(map);
        IncidentDef def;
        switch (raid.source)
        {
            case RaidSource.Insects:
                def = IncidentDefOf.Infestation;
                parms.points = (float)SiteLinkMath.RetaliationPoints(now, raid.pointsFactor, NonRaidFloor);
                break;
            case RaidSource.Manhunters:
                def = IncidentDefOf.ManhunterPack;
                parms.pawnKind = raid.animal;
                parms.points = (float)SiteLinkMath.RetaliationPoints(now, raid.pointsFactor, NonRaidFloor);
                break;
            default:
                Faction? f = raid.source switch
                {
                    RaidSource.Mechanoids => Faction.OfMechanoids,
                    RaidSource.Pirates => Faction.OfPirates,
                    _ => raid.faction,
                };
                if (f == null || f.defeated || !f.HostileTo(Faction.OfPlayer)) f = Find.FactionManager.RandomEnemyFaction();
                if (f == null) return true; // nobody left to send: the debt is void
                def = IncidentDefOf.RaidEnemy;
                parms.faction = f;
                parms.points = (float)SiteLinkMath.RetaliationPoints(now, raid.pointsFactor,
                    f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
                break;
        }
        return def.Worker.TryExecute(parms);
    }
}
