// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public readonly record struct LootStack(double UnitValue, double UnitMass, int Count);

public static class LootFit
{
    // How many of each stack the squad carries home: massless stacks whole, the rest by value per kilogram,
    // highest first, filling partially until the capacity runs out.
    public static int[] Fit(IReadOnlyList<LootStack> stacks, double capacityKg)
    {
        var kept = new int[stacks.Count];
        double left = Math.Max(0, capacityKg);
        var order = Enumerable.Range(0, stacks.Count)
            .Where(i => stacks[i].UnitMass > 0)
            .OrderByDescending(i => stacks[i].UnitValue / stacks[i].UnitMass);
        for (int i = 0; i < stacks.Count; i++)
            if (stacks[i].UnitMass <= 0) kept[i] = stacks[i].Count;
        foreach (int i in order)
        {
            int take = (int)Math.Min(stacks[i].Count, Math.Floor(left / stacks[i].UnitMass));
            kept[i] = take;
            left -= take * stacks[i].UnitMass;
        }
        return kept;
    }
}
