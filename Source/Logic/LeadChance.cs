// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public static class LeadChance
{
    // techLevel is vanilla TechLevel as an int: Undefined 0, Animal 1, Neolithic 2, Medieval 3, Industrial 4,
    // Spacer 5, Ultra 6, Archotech 7. Rarer and more advanced items are harder to find a lead for.
    public static double Of(int techLevel, double marketValue, Curve techCurve, Curve valueCurve) =>
        Math.Clamp(techCurve.Evaluate(techLevel) * valueCurve.Evaluate(marketValue), 0.01, 0.99);
}
