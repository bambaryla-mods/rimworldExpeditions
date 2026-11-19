// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

// Piecewise-linear curve, clamped to its end values. Defs hold vanilla SimpleCurves; this is what Logic computes with.
public sealed class Curve
{
    private readonly (double x, double y)[] points;

    public Curve(IReadOnlyList<(double x, double y)> points)
    {
        if (points.Count == 0) throw new ArgumentException("A curve needs at least one point.", nameof(points));
        this.points = points.OrderBy(p => p.x).ToArray();
    }

    public double Evaluate(double x)
    {
        if (x <= points[0].x) return points[0].y;
        for (int i = 1; i < points.Length; i++)
        {
            var (x1, y1) = points[i];
            if (x > x1) continue;
            var (x0, y0) = points[i - 1];
            return x1 == x0 ? y1 : y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }
        return points[^1].y;
    }
}
