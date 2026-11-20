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

    public int Seed => id * 7919 + Find.World.info.Seed;

    public string Label
    {
        get
        {
            if (captive != null) return "Exp_RescueLabel".Translate(captive.LabelShortCap);
            var notes = new List<string>();
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
    }
}
