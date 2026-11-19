// Logic: no Verse, RimWorld or UnityEngine here.
namespace Expeditions;

public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(Dot(this));
    public Vec3 Normalized => Length > 0 ? this * (1 / Length) : this;
}

public static class Projection
{
    // Azimuthal equidistant projection around `center`: distance from the center on the flat map equals the arc
    // distance on the sphere, in radians. North (the direction toward `north` from the center) is +y.
    public static (double x, double y) AzimuthalEquidistant(Vec3 center, Vec3 point, Vec3 north)
    {
        Vec3 c = center.Normalized, p = point.Normalized;
        double d = Math.Acos(Math.Clamp(c.Dot(p), -1, 1));
        if (d < 1e-12) return (0, 0);
        Vec3 t = (p - c * c.Dot(p)).Normalized;
        Vec3 nT = (north - c * c.Dot(north)).Normalized;
        Vec3 e = nT.Cross(c);
        return (d * t.Dot(e), d * t.Dot(nT));
    }
}
