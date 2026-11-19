// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public static class FoodMath
{
    // Nutrition the whole squad needs for the trip: each member's daily nutrition times the trip's days.
    public static double Required(IReadOnlyList<double> dailyNutrition, double tripDays) => dailyNutrition.Sum() * tripDays;

    public static double Shortfall(double required, double available) => Math.Max(0, required - available);
}
