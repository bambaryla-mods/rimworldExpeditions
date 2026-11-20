// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public static class WeightedOrder
{
    // Indices in a weighted random order: heavier entries tend to come first, and entries with no weight are left
    // out. Efraimidis-Spirakis: each entry draws u in [0, 1) and sorts by u^(1/weight), highest first.
    public static List<int> Order(IReadOnlyList<double> weights, Func<double> random01)
    {
        var keyed = new List<(double key, int index)>();
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0) continue;
            keyed.Add((Math.Pow(random01(), 1.0 / weights[i]), i));
        }
        return keyed.OrderByDescending(k => k.key).Select(k => k.index).ToList();
    }
}
