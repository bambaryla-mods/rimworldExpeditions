using RimWorld;
using Verse;

namespace Expeditions;

// Which items can be prospected for, and the chance of a lead for each.
public static class ProspectCatalog
{
    private static List<ThingDef>? all;

    public static IReadOnlyList<ThingDef> All => all ??= DefDatabase<ThingDef>.AllDefsListForReading
        .Where(Eligible).OrderBy(d => d.label).ToList();

    public static bool Eligible(ThingDef def)
    {
        if (def.category != ThingCategory.Item || !def.EverHaulable || def.IsCorpse) return false;
        if (def.tradeability == Tradeability.None || def.BaseMarketValue <= 0f || def.destroyOnDrop) return false;
        foreach (ExpeditionExclusionDef ex in DefDatabase<ExpeditionExclusionDef>.AllDefsListForReading)
        {
            if (ex.things.Contains(def)) return false;
            if (CategoriesOf(def).Any(ex.categories.Contains)) return false;
        }
        return true;
    }

    // The item's categories and all their parents.
    public static IEnumerable<ThingCategoryDef> CategoriesOf(ThingDef def)
    {
        if (def.thingCategories == null) yield break;
        foreach (ThingCategoryDef c in def.thingCategories)
        {
            yield return c;
            foreach (ThingCategoryDef p in c.Parents) yield return p;
        }
    }

    public static double Chance(ThingDef def)
    {
        var t = ExpeditionsDefOf.Tuning;
        return LeadChance.Of((int)def.techLevel, def.BaseMarketValue, t.TechCurve, t.ValueCurve);
    }
}
