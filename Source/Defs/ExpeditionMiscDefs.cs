using RimWorld;
using UnityEngine;
using Verse;

namespace Expeditions;

// Items that can never be prospected, on top of the built-in rules in ProspectCatalog.
public class ExpeditionExclusionDef : Def
{
    public List<ThingDef> things = new();
    public List<ThingCategoryDef> categories = new();
}

public class BiomeColor
{
    public BiomeDef biome = null!;
    public Color color = Color.gray;
}

// All tuning numbers in one def, Expeditions_Tuning, so they can be patched.
public class ExpeditionTuningDef : Def
{
    public SimpleCurve challengeRatioCurve = new();
    public SimpleCurve techLevelCurve = new();
    public SimpleCurve valueCurve = new();
    public float partialBand = 0.15f;

    public IntRange offerIntervalTicks = new(60000, 180000);
    public IntRange offerLifetimeTicks = new(180000, 480000);
    public IntRange prospectLifetimeTicks = new(300000, 480000);

    public float loudStealthCap = 30f;
    public float armedFightBonus = 60f;
    public float armedSpread = 0.5f;

    public float flightBaseDays = 0.25f;
    public float flightDaysPerTile = 0.01f;
    public float vehicleFootFactor = 0.5f;

    public int prospectBaseTicks = 15000;
    public float xpPerChallengeWeight = 800f;

    public List<BiomeColor> biomeColors = new();

    private Curve? ratio, tech, value;

    public Curve RatioCurve => ratio ??= ToCurve(challengeRatioCurve);
    public Curve TechCurve => tech ??= ToCurve(techLevelCurve);
    public Curve ValueCurve => value ??= ToCurve(valueCurve);
    public TravelTuning Travel => new(flightBaseDays, flightDaysPerTile, vehicleFootFactor);

    private static Curve ToCurve(SimpleCurve c) => new(c.Points.Select(p => ((double)p.x, (double)p.y)).ToList());

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string e in base.ConfigErrors()) yield return e;
        if (challengeRatioCurve.PointsCount == 0) yield return "challengeRatioCurve has no points";
        if (techLevelCurve.PointsCount == 0) yield return "techLevelCurve has no points";
        if (valueCurve.PointsCount == 0) yield return "valueCurve has no points";
    }
}

[DefOf]
public static class ExpeditionsDefOf
{
    public static ExpeditionTuningDef Expeditions_Tuning = null!;
    public static ExpeditionChallengeDef Exp_Fight = null!;
    public static ExpeditionChallengeDef Exp_Stealth = null!;
    public static ExpeditionChallengeDef Exp_Haul = null!;
    public static ExpeditionSiteDef Exp_HoldingCamp = null!;
    public static ExpeditionSiteDef Exp_Rumor = null!;

    static ExpeditionsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ExpeditionsDefOf));

    public static ExpeditionTuningDef Tuning => Expeditions_Tuning;
}
