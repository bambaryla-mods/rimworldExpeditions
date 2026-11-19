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

    public Faction? faction;
    public Pawn? captive;

    public int Seed => id * 7919 + Find.World.info.Seed;

    public string Label => captive != null
        ? "Exp_RescueLabel".Translate(captive.LabelShortCap)
        : target != null ? $"{site.LabelCap} ({target.label})" : site.LabelCap.ToString();

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
        Scribe_References.Look(ref captive, "captive");
    }
}
