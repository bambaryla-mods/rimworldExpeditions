// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// Ideology work sites: what a squad takes of the stash, and how hard the guards make the fight.
public static class WorksiteMath
{
    // The squad's cut of one stash item. Partial halves it (at least 1, if there was anything to take).
    public static int Share(int stash, double share, Outcome outcome)
    {
        int best = (int)Math.Floor(stash * share);
        return outcome switch
        {
            Outcome.Success => best,
            Outcome.Partial => best == 0 ? 0 : Math.Max(1, (int)Math.Round(best * 0.5)),
            _ => 0,
        };
    }

    // A squad that turns back eats for the days it is actually away (there and home again), never more than it packed.
    public static double TurnBackFood(double food, double awayDays, double tripDays) =>
        tripDays <= 0 ? food : food * Math.Clamp(awayDays / tripDays, 0, 1);

    // Fight is a Sum challenge, so each guard adds to what the squad must bring.
    public static double FightRequirement(int guards, double perGuard, double min = 100) => Math.Max(min, guards * perGuard);
}
