using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Expeditions;

// The colony's own flat map: world tiles in range, projected around the home tile (distance on the map matches
// distance in the world), baked once into a texture per style. Markers are drawn live on top by the window.
public class BakedMap
{
    public Texture2D texture = null!;
    public int size;
    public float pxPerRadian;
    public Dictionary<PlanetTile, Vector2> texPixel = new();
    public PlanetTile[] owner = Array.Empty<PlanetTile>();

    // 0..1 across the map, y down (GUI space). Tiles outside the bake come back clamped to the edge.
    public Vector2 ToUV(PlanetTile tile)
    {
        if (!texPixel.TryGetValue(tile, out Vector2 p)) p = MapBaker.ProjectPixel(this, tile);
        return new Vector2(Mathf.Clamp01(p.x / size), Mathf.Clamp01(1f - p.y / size));
    }

    public PlanetTile TileAt(Vector2 uv)
    {
        int x = Mathf.Clamp((int)(uv.x * size), 0, size - 1);
        int y = Mathf.Clamp((int)((1f - uv.y) * size), 0, size - 1);
        return owner[y * size + x];
    }

    internal Vec3 center;
    internal Vec3 north;
}

public static class MapBaker
{
    public const int Size = 1024;
    private const float Fill = 0.95f;
    private const float ContourMeters = 250f;

    private static readonly Dictionary<(PlanetTile, int, MapStyle), BakedMap> cache = new();

    public static void ClearCache()
    {
        foreach (BakedMap m in cache.Values) UnityEngine.Object.Destroy(m.texture);
        cache.Clear();
    }

    public static BakedMap Get(PlanetTile home, int range, MapStyle style)
    {
        if (!cache.TryGetValue((home, range, style), out BakedMap map))
            cache[(home, range, style)] = map = Bake(home, range, style);
        return map;
    }

    internal static Vector2 ProjectPixel(BakedMap m, PlanetTile tile)
    {
        var (x, y) = Projection.AzimuthalEquidistant(m.center, ToVec(Find.WorldGrid.GetTileCenter(tile)), m.north);
        return new Vector2(m.size / 2f + (float)x * m.pxPerRadian, m.size / 2f + (float)y * m.pxPerRadian);
    }

    private static Vec3 ToVec(Vector3 v) => new(v.x, v.y, v.z);

    private static BakedMap Bake(PlanetTile home, int range, MapStyle style)
    {
        var tiles = WorldArea.Distances(home, range).Keys.ToList();
        var m = new BakedMap
        {
            size = Size,
            center = ToVec(Find.WorldGrid.GetTileCenter(home)),
            north = ToVec(home.Layer.NorthPolePos),
            owner = Enumerable.Repeat(PlanetTile.Invalid, Size * Size).ToArray(),
        };

        // Scale so the farthest tile lands at 95% of the half size.
        var raw = tiles.Select(t => Projection.AzimuthalEquidistant(m.center, ToVec(Find.WorldGrid.GetTileCenter(t)), m.north)).ToList();
        double maxR = raw.Count == 0 ? 1 : raw.Max(p => Math.Sqrt(p.x * p.x + p.y * p.y));
        m.pxPerRadian = (float)(Size / 2f * Fill / Math.Max(maxR, 1e-6));
        for (int i = 0; i < tiles.Count; i++)
            m.texPixel[tiles[i]] = new Vector2(Size / 2f + (float)raw[i].x * m.pxPerRadian, Size / 2f + (float)raw[i].y * m.pxPerRadian);

        // Each pixel takes the nearest tile, by splatting a disc per tile into a distance buffer.
        float radius = TileRadiusPx(home, m) * 1.4f;
        int r = Math.Max(2, (int)Math.Ceiling(radius));
        var best = Enumerable.Repeat(float.MaxValue, Size * Size).ToArray();
        foreach (PlanetTile t in tiles)
        {
            Vector2 c = m.texPixel[t];
            int cx = (int)c.x, cy = (int)c.y;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                float d = (x + 0.5f - c.x) * (x + 0.5f - c.x) + (y + 0.5f - c.y) * (y + 0.5f - c.y);
                if (d > radius * radius) continue;
                int i = y * Size + x;
                if (d >= best[i]) continue;
                best[i] = d;
                m.owner[i] = t;
            }
        }

        var px = style == MapStyle.Holo ? PaintHolo(m) : PaintVanilla(m);
        DrawLinks(m, px, tiles, style);
        m.texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        m.texture.SetPixels32(px);
        m.texture.Apply(false, false);
        return m;
    }

    private static float TileRadiusPx(PlanetTile home, BakedMap m)
    {
        var n = new List<PlanetTile>();
        Find.WorldGrid.GetTileNeighbors(home, n);
        if (n.Count == 0) return 3f;
        Vec3 c = m.center.Normalized;
        double angle = n.Average(t => Math.Acos(Math.Clamp(c.Dot(ToVec(Find.WorldGrid.GetTileCenter(t)).Normalized), -1, 1)));
        return Math.Max(1.5f, (float)(angle * m.pxPerRadian / 2));
    }

    private static bool IsWater(PlanetTile t) => !t.Valid || Find.WorldGrid[t].WaterCovered;

    private static Color32[] PaintVanilla(BakedMap m)
    {
        var px = new Color32[Size * Size];
        var water = new Color32(42, 74, 99, 255);
        var outside = new Color32(21, 23, 26, 255);
        for (int i = 0; i < px.Length; i++)
        {
            PlanetTile t = m.owner[i];
            if (!t.Valid) { px[i] = outside; continue; }
            Tile tile = Find.WorldGrid[t];
            if (tile.WaterCovered) { px[i] = water; continue; }
            Color c = BiomeColors.For(tile.PrimaryBiome);
            float shade = tile.HillinessLabel switch { Hilliness.LargeHills => 0.85f, Hilliness.Mountainous => 0.7f, Hilliness.Impassable => 0.55f, _ => 1f };
            px[i] = (Color32)((c * shade) with { a = 1f });
        }
        return px;
    }

    private static Color32[] PaintHolo(BakedMap m)
    {
        var px = new Color32[Size * Size];
        var bg = new Color(0.024f, 0.071f, 0.094f);
        var land = new Color(0.043f, 0.165f, 0.2f);
        var grid = new Color(0.114f, 0.29f, 0.353f);
        var coast = new Color(0.247f, 0.816f, 0.941f);
        var contour = new Color(0.165f, 0.639f, 0.753f);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int i = y * Size + x;
            PlanetTile t = m.owner[i];
            Color c = bg;
            if (x % 40 == 0 || y % 40 == 0) c = grid;
            if (t.Valid && !IsWater(t))
            {
                Tile tile = Find.WorldGrid[t];
                c = Color.Lerp(land, BiomeColors.For(tile.PrimaryBiome), 0.1f);
                bool edge = (x > 0 && IsWater(m.owner[i - 1])) || (x < Size - 1 && IsWater(m.owner[i + 1]))
                    || (y > 0 && IsWater(m.owner[i - Size])) || (y < Size - 1 && IsWater(m.owner[i + Size]));
                if (edge) c = coast;
                else if (ContourAt(m, i, x, y, tile.elevation)) c = Color.Lerp(c, contour, 0.55f);
            }
            px[i] = (Color32)(c with { a = 1f });
        }
        return px;
    }

    private static bool ContourAt(BakedMap m, int i, int x, int y, float elevation)
    {
        int band = Mathf.FloorToInt(elevation / ContourMeters);
        bool Differs(int j) => m.owner[j].Valid && !IsWater(m.owner[j]) && Mathf.FloorToInt(Find.WorldGrid[m.owner[j]].elevation / ContourMeters) != band;
        return (x < Size - 1 && Differs(i + 1)) || (y < Size - 1 && Differs(i + Size));
    }

    private static void DrawLinks(BakedMap m, Color32[] px, List<PlanetTile> tiles, MapStyle style)
    {
        Color32 river = style == MapStyle.Holo ? new Color32(63, 140, 255, 255) : new Color32(93, 143, 179, 255);
        Color32 road = style == MapStyle.Holo ? new Color32(63, 208, 240, 150) : new Color32(156, 138, 102, 255);
        foreach (PlanetTile t in tiles)
        {
            if (Find.WorldGrid[t] is not SurfaceTile s) continue;
            if (s.Rivers != null)
                foreach (var link in s.Rivers) Line(m, px, t, link.neighbor, river, 2, false);
            if (s.Roads != null)
                foreach (var link in s.Roads) Line(m, px, t, link.neighbor, road, style == MapStyle.Holo ? 1 : 2, style == MapStyle.Holo);
        }
    }

    private static void Line(BakedMap m, Color32[] px, PlanetTile a, PlanetTile b, Color32 color, int width, bool dashed)
    {
        if (!m.texPixel.TryGetValue(a, out Vector2 p0) || !m.texPixel.TryGetValue(b, out Vector2 p1)) return;
        int steps = Mathf.CeilToInt(Vector2.Distance(p0, p1));
        for (int s = 0; s <= steps; s++)
        {
            if (dashed && s % 6 > 3) continue;
            Vector2 p = Vector2.Lerp(p0, p1, steps == 0 ? 0 : s / (float)steps);
            for (int w = 0; w < width; w++)
            for (int v = 0; v < width; v++)
            {
                int x = (int)p.x + w, y = (int)p.y + v;
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                px[y * Size + x] = color;
            }
        }
    }
}
