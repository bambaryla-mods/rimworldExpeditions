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

    public float worksiteShare = 0.6f;
    public float worksiteFightPerGuard = 90f;

    // Who can go, and what happens on the road.
    public float contractMarginDays = 1f;
    public SimpleCurve slaveRunawayCurve = new();
    public float turnedCreepjoinerFactor = 1.5f;
    public float fleshbeastFight = 150f;
    public float sightstealerPointsFactor = 0.45f;
    public float slipAwayRequirement = 100f;
    public SimpleCurve fightWinCurve = new();
    public SimpleCurve fightWoundCurve = new();
    public SimpleCurve fightDeathCurve = new();

    public List<BiomeColor> biomeColors = new();

    private Curve? ratio, tech, value, runaway, fightWin, fightWound, fightDeath;

    public Curve RatioCurve => ratio ??= ToCurve(challengeRatioCurve);
    public Curve TechCurve => tech ??= ToCurve(techLevelCurve);
    public Curve ValueCurve => value ??= ToCurve(valueCurve);
    public Curve SlaveRunawayCurve => runaway ??= ToCurve(slaveRunawayCurve);
    public Curve FightWinCurve => fightWin ??= ToCurve(fightWinCurve);
    public Curve FightWoundCurve => fightWound ??= ToCurve(fightWoundCurve);
    public Curve FightDeathCurve => fightDeath ??= ToCurve(fightDeathCurve);
    public TravelTuning Travel => new(flightBaseDays, flightDaysPerTile, vehicleFootFactor);

    private static Curve ToCurve(SimpleCurve c) => new(c.Points.Select(p => ((double)p.x, (double)p.y)).ToList());

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string e in base.ConfigErrors()) yield return e;
        if (challengeRatioCurve.PointsCount == 0) yield return "challengeRatioCurve has no points";
        if (techLevelCurve.PointsCount == 0) yield return "techLevelCurve has no points";
        if (valueCurve.PointsCount == 0) yield return "valueCurve has no points";
        if (slaveRunawayCurve.PointsCount == 0) yield return "slaveRunawayCurve has no points";
        if (fightWinCurve.PointsCount == 0) yield return "fightWinCurve has no points";
        if (fightWoundCurve.PointsCount == 0) yield return "fightWoundCurve has no points";
        if (fightDeathCurve.PointsCount == 0) yield return "fightDeathCurve has no points";
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
    [MayRequireIdeology] public static ExpeditionSiteDef? Exp_WorkSite;
    public static JobDef Exp_Prospect = null!;
    public static JobDef Exp_Depart = null!;
    public static LetterDef Exp_ReportLetter = null!;

    static ExpeditionsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ExpeditionsDefOf));

    public static ExpeditionTuningDef Tuning => Expeditions_Tuning;
}
