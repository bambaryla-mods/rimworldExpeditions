using RimWorld;
using Verse;

namespace Expeditions;

// What one faction changes about the sites it is part of: what it pays in as a patron, and what it holds (or does)
// at named sites. At most one per FactionDef. While any gate is closed the whole override is off, so tier goods stay
// away from a colony that hasn't reached the tier.
public class ExpeditionFactionDef : Def
{
    public FactionDef faction = null!;
    public List<SiteGate> gates = new();
    public List<ThingDef> payment = new(); // as patron: silverReward paid in these, at equal market value
    public List<FactionSiteLoot> siteLoot = new();
    public List<FactionSiteEffects> outcomeEffects = new();

    private static Dictionary<FactionDef, ExpeditionFactionDef>? byFaction;

    public bool Live => gates.All(g => Effects.Safe(this, () => g.Open(), false));

    // The live override for this faction, or null.
    public static ExpeditionFactionDef? For(Faction? f)
    {
        if (f == null) return null;
        byFaction ??= DefDatabase<ExpeditionFactionDef>.AllDefsListForReading.Where(d => d.faction != null)
            .GroupBy(d => d.faction).ToDictionary(g => g.Key, g => g.First());
        return byFaction.TryGetValue(f.def, out ExpeditionFactionDef? d) && d!.Live ? d : null;
    }

    public FactionSiteLoot? LootFor(ExpeditionSiteDef site) => siteLoot.FirstOrDefault(l => l.site == site);

    public IEnumerable<OutcomeEffect> EffectsFor(ExpeditionSiteDef site) =>
        outcomeEffects.Where(x => x.site == site).SelectMany(x => x.effects);

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string e in base.ConfigErrors()) yield return e;
        if (faction == null) yield return "has no faction";
        else if (DefDatabase<ExpeditionFactionDef>.AllDefsListForReading.Count(d => d.faction == faction) > 1)
            yield return $"is one of several ExpeditionFactionDefs for {faction.defName}; only the first is used";
        foreach (SiteGate g in gates)
        {
            if (g == null) { yield return "has a null gate"; continue; }
            foreach (string e in g.ConfigErrors()) yield return e;
        }
        foreach (FactionSiteLoot l in siteLoot)
        {
            if (l.site == null) yield return "has a siteLoot entry with no site";
            foreach (LootEntry entry in l.loot)
                if ((entry.thing == null) == (entry.category == null)) yield return "has a loot entry that needs exactly one of thing or category";
        }
        foreach (FactionSiteEffects x in outcomeEffects)
            if (x.site == null) yield return "has an outcomeEffects entry with no site";
        foreach (ThingDef t in payment)
            if (t == null || t.BaseMarketValue <= 0f) yield return "has a payment thing that is missing or has no market value";
    }
}

public class FactionSiteLoot
{
    public ExpeditionSiteDef site = null!;
    public bool replace; // instead of the site's own table, else added to it
    public List<LootEntry> loot = new();
}

public class FactionSiteEffects
{
    public ExpeditionSiteDef site = null!;
    public List<OutcomeEffect> effects = new();
}
