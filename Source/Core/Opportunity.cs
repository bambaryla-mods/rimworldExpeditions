using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Expeditions;

// A point of interest on the colony's own map. It exists only in this mod; nothing is placed on the world map.
public class Opportunity : IExposable
{
    public int id;
    public ExpeditionSiteDef site = null!;
    public PlanetTile tile = PlanetTile.Invalid;
    public int expiresTick = -1;

    public bool prospected;
    public ThingDef? target;
    public int targetCount;

    public Faction? faction; // the site's own faction, or the patron for a job
    public Faction? enemy;   // who the squad fights for the patron (sites with needsEnemy)
    public Pawn? captive;
    public Site? linkedSite; // the vanilla quest site on the world map this offer is for
    public Settlement? linkedSettlement; // the real settlement this offer is for (sites with settlementLink)

    public int Seed => id * 7919 + Find.World.info.Seed;

    // A linked site's own strength sets its Fight requirement; every other challenge asks what the def says.
    public float Requirement(SiteChallenge sc)
    {
        if (linkedSite != null && sc.challenge == ExpeditionsDefOf.Exp_Fight && site.LinkWorker is { } w)
            return w.FightRequirement(linkedSite);
        if (linkedSettlement != null && sc.challenge == ExpeditionsDefOf.Exp_Fight && site.SettlementWorker is { } sw)
            return sw.FightRequirement(linkedSettlement);
        return sc.requirement;
    }

    public string Label
    {
        get
        {
            if (captive != null) return "Exp_RescueLabel".Translate(captive.LabelShortCap);
            if (linkedSite != null)
                return faction != null ? $"{linkedSite.LabelCap} ({faction.Name})" : linkedSite.LabelCap;
            var notes = new List<string>();
            if (linkedSettlement != null) notes.Add(linkedSettlement.Label);
            if (target != null) notes.Add(target.label);
            if (enemy != null && faction != null) notes.Add("Exp_LabelEnemyFor".Translate(enemy.Name, faction.Name));
            else if (faction != null) notes.Add(faction.Name);
            return notes.Count == 0 ? site.LabelCap.ToString() : $"{site.LabelCap} ({string.Join(", ", notes)})";
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref id, "id");
        Scribe_Defs.Look(ref site, "site");
        Scribe_Values.Look(ref tile, "tile", PlanetTile.Invalid);
        Scribe_Values.Look(ref expiresTick, "expiresTick", -1);
        Scribe_Values.Look(ref prospected, "prospected");
        Scribe_Defs.Look(ref target, "target");
        Scribe_Values.Look(ref targetCount, "targetCount");
        Scribe_References.Look(ref faction, "faction");
        Scribe_References.Look(ref enemy, "enemy");
        Scribe_References.Look(ref captive, "captive");
        Scribe_References.Look(ref linkedSite, "worksite"); // the label from when only work sites were linked
        Scribe_References.Look(ref linkedSettlement, "settlement");
    }
}
