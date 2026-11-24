using RimWorld;
using Verse;

namespace Expeditions;

// Vanilla's item analysis (the kind that unlocks research), for loot that stops once found another way.
public static class Analysis
{
    public static int Id(ThingDef def) => def.GetCompProperties<CompProperties_CompAnalyzableUnlockResearch>()?.analysisID ?? -1;

    public static bool Analyzable(ThingDef def) => Id(def) >= 0;

    // Vanilla's IsAnalysisComplete is true for an item that never existed, so only a recorded, satisfied analysis counts.
    public static bool Done(ThingDef def) =>
        Analyzable(def) && Find.AnalysisManager.TryGetAnalysisProgress(Id(def), out AnalysisDetails details) && details.Satisfied;
}
