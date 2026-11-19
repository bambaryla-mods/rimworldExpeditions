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

    public override string SettingsCategory() => "Expeditions";

    public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
    {
        var list = new Listing_Standard();
        list.Begin(inRect);
        list.Label("Exp_SettingStyle".Translate());
        foreach (MapStyle style in Enum.GetValues<MapStyle>())
            if (list.RadioButton(("Exp_Style_" + style).Translate(), Settings.style == style)) Settings.style = style;
        list.Gap();
        int range = Settings.range;
        list.Label("Exp_SettingRange".Translate(range));
        range = (int)list.Slider(range, 20, 120);
        if (range != Settings.range)
        {
            Settings.range = range;
            // A new range means a new area and a new map bake.
            WorldArea.ClearCache();
            MapBaker.ClearCache();
        }
        list.Gap();
        list.Label("Exp_SettingOffers".Translate(Settings.offerSlots));
        Settings.offerSlots = (int)list.Slider(Settings.offerSlots, 1, 10);
        list.End();
    }
}
