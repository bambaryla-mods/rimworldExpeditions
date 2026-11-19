using HarmonyLib;
using RimWorld;
using Verse;

namespace Expeditions;

// Away pawns sit in the mod's own holder, not in a caravan, so vanilla would forget them. These patches keep them
// counted as the colony's.
//
// Patch list (keep it current):
// - PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead: every "all colonists" list in 1.7 builds on it.
// - GameEnder.CheckOrUpdateGameOver: only counts pawns on maps, so a colony with everyone away would end.
// - ColonistBar.CheckRecacheEntries: shows away pawns, one group per expedition.
[HarmonyPatch(typeof(PawnsFinder), nameof(PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead), MethodType.Getter)]
public static class Patch_PawnsFinder_AllCaravans
{
    public static void Postfix(List<Pawn> __result)
    {
        if (ExpeditionsGame.Instance == null || Current.Game == null) return;
        foreach (Pawn p in ExpeditionsGame.Instance.AllAwayPawns)
            if (!__result.Contains(p)) __result.Add(p);
    }
}

[HarmonyPatch(typeof(GameEnder), nameof(GameEnder.CheckOrUpdateGameOver))]
public static class Patch_GameEnder_CheckOrUpdateGameOver
{
    public static bool Prefix(GameEnder __instance)
    {
        if (ExpeditionsGame.Instance == null) return true;
        if (!ExpeditionsGame.Instance.AllAwayPawns.Any(p => !p.Dead && p.IsFreeColonist)) return true;
        __instance.gameEnding = false;
        return false;
    }
}

[HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
public static class Patch_ColonistBar_CheckRecacheEntries
{
    public static void Prefix(bool ___entriesDirty, out bool __state) => __state = ___entriesDirty;

    public static void Postfix(bool __state, List<ColonistBar.Entry> ___cachedEntries)
    {
        if (!__state || ExpeditionsGame.Instance == null || !Find.PlaySettings.showColonistBar) return;
        int group = ___cachedEntries.Count == 0 ? 0 : ___cachedEntries.Max(e => e.group) + 1;
        foreach (Expedition e in ExpeditionsGame.Instance.active)
        {
            var pawns = e.AwayPawns.Where(p => p.IsColonist).ToList();
            if (pawns.Count == 0) continue;
            foreach (Pawn p in pawns)
            {
                // Reordering and dragging look the group up by map or caravan; an expedition group has neither.
                var entry = new ColonistBar.Entry(p, null, group)
                {
                    reorderAction = (_, _) => { },
                    extraDraggedItemOnGUI = (_, _) => { },
                };
                ___cachedEntries.Add(entry);
            }
            group++;
        }
    }
}
