using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Expeditions;

// Which faction a site needs nearby: a non-hidden faction with that relation and a settlement in range.
public enum FactionNeed
{
    None,
    Hostile,
    Ally,
    Friendly, // not hostile: neutral or ally
}

public class SiteChallenge
{
    public ExpeditionChallengeDef challenge = null!;
    public float weight = 1f;
    public float requirement = 100f;
}

// Exactly one of thing or category. A category picks a random eligible item from it at roll time.
public class LootEntry
{
    public ThingDef? thing;
    public ThingCategoryDef? category;
    public IntRange count = new(1, 1);
    public float chance = 1f;
    public bool successOnly; // rolled only on a full Success, for single rare items
}

// People who come home with the squad and join the colony.
public class JoinerSpec
{
    public float chance = 1f;
    public IntRange count = new(1, 1);
    public PawnKindDef? kind;
}

// Wild animals of the site's biome, tamed and brought home.
public class TamedAnimalsSpec
{
    public float chance = 1f;
    public IntRange count = new(1, 1);
    public float maxWildness = 1f;

    // The biome's wild animals this site can tame.
    public List<PawnKindDef> Candidates(BiomeDef biome) =>
        biome.AllWildAnimals
            .Where(k => k.RaceProps.Animal && biome.CommonalityOfAnimal(k) > 0f && k.race.GetStatValueAbstract(StatDefOf.Wildness) <= maxWildness)
            .ToList();
}

public class FateEntry
{
    public float unharmed = 1f;
    public float injured;
    public float killed;
    public float captured;

    public FateWeights ToWeights() => new(unharmed, injured, killed, captured);
}

public class RiskProfile
{
    public FateEntry success = new() { unharmed = 0.8f, injured = 0.2f };
    public FateEntry partial = new() { unharmed = 0.5f, injured = 0.4f, killed = 0.07f, captured = 0.03f };
    public FateEntry failure = new() { unharmed = 0.1f, injured = 0.5f, killed = 0.2f, captured = 0.2f };

    public FateWeights For(Outcome outcome) => outcome switch
    {
        Outcome.Success => success.ToWeights(),
        Outcome.Partial => partial.ToWeights(),
        _ => failure.ToWeights(),
    };
}

// A kind of point of interest: where it can appear, what it asks of a squad, what it holds and how it can go wrong.
public class ExpeditionSiteDef : Def
{
    public string? iconPath;
    public Color color = Color.white;

    public List<BiomeDef> biomes = new();
    public List<Hilliness> hilliness = new();
    public bool needsRiver;
    public bool needsRoad;
    public bool needsCoast;
    public FactionNeed needsFaction = FactionNeed.None;
    public bool needsEnemy; // a hostile settlement near the tile too: who the squad fights for the patron
    public bool growingSeason;
    public float commonness = 1f; // weight among ready-made offers

    public List<SiteChallenge> challenges = new();
    public List<LootEntry> loot = new();
    public float findChanceBase = 0.6f;
    public RiskProfile risk = new();
    public float daysOnSite = 0.5f;
    public float openness = 1f;
    public IntRange squadSize = new(1, 6);

    public List<ThingCategoryDef> servesCategories = new();
    public bool rescueOnly;
    public bool fallback;

    public int goodwillReward;
    public int goodwillAllFriendly;
    public int silverReward;
    public float xpFactor = 1f;
    public JoinerSpec? joiners;
    public TamedAnimalsSpec? tamedAnimals;
    [MustTranslate] public string? successText; // a line in the report on a full Success, for jobs with nothing to bring home

    // Whether there is anything to find here. Without it the report says nothing about finding.
    public bool HasFinds => loot.Count > 0 || joiners != null || tamedAnimals != null;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string e in base.ConfigErrors()) yield return e;
        if (challenges.NullOrEmpty()) yield return "has no challenges";
        foreach (SiteChallenge c in challenges)
            if (c.challenge == null) yield return "has a challenge entry with no challenge";
        if (squadSize.min < 1) yield return $"squadSize.min is {squadSize.min}, it must be at least 1";
        if (squadSize.max < squadSize.min) yield return "squadSize.max is below squadSize.min";
        foreach (ThingCategoryDef c in servesCategories)
            if (c == null) yield return "has a missing servesCategories entry";
        foreach (LootEntry l in loot)
            if ((l.thing == null) == (l.category == null)) yield return "has a loot entry that needs exactly one of thing or category";
        if (commonness < 0f) yield return $"commonness is {commonness}, it must not be negative";
        if (xpFactor < 0f) yield return $"xpFactor is {xpFactor}, it must not be negative";
        if (joiners != null && joiners.kind == null) yield return "joiners has no kind";
        if (joiners != null && joiners.count.min < 1) yield return "joiners.count.min must be at least 1";
        if (tamedAnimals != null && tamedAnimals.count.min < 1) yield return "tamedAnimals.count.min must be at least 1";
    }
}
