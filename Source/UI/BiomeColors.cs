using RimWorld;
using UnityEngine;
using Verse;

namespace Expeditions;

// Map colors per biome: the tuning table, or a muted color hashed from the defName for modded biomes.
public static class BiomeColors
{
    private static Dictionary<BiomeDef, Color>? table;

    public static Color For(BiomeDef? biome)
    {
        if (biome == null) return new Color(0.3f, 0.3f, 0.3f);
        table ??= ExpeditionsDefOf.Tuning.biomeColors.Where(b => b.biome != null).ToDictionary(b => b.biome, b => b.color);
        if (table.TryGetValue(biome, out Color c)) return c;
        uint h = 2166136261;
        foreach (char ch in biome.defName) { h ^= ch; h *= 16777619; }
        return Color.HSVToRGB(h % 360 / 360f, 0.35f, 0.55f);
    }
}
