using HarmonyLib;
using Verse;

namespace Expeditions;

public class ExpeditionsSettings : ModSettings
{
    public MapStyle style = MapStyle.Vanilla;
    public int range = 60;
    public int offerSlots = 5;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref style, "style", MapStyle.Vanilla);
        Scribe_Values.Look(ref range, "range", 60);
        Scribe_Values.Look(ref offerSlots, "offerSlots", 5);
    }
}

public class ExpeditionsMod : Mod
{
    public const string HarmonyId = "bambaryla.expeditions";

    public static ExpeditionsSettings Settings = null!;

    public ExpeditionsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ExpeditionsSettings>();
        new Harmony(HarmonyId).PatchAll();
    }
}
