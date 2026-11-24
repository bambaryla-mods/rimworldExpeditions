# Expeditions: hooks for other mods, and four new sites

Expeditions gets the pieces another mod needs to add its own sites without patching Expeditions code, plus four new generic sites that came out of planning Beyond Our Reach (BoR) sites and work in any game. BoR's own sites are in `BeyondOurReach/openspec/expeditions-sites-design.md` and build on this.

## Intent
BoR wants a dozen sites tied to its tiers, factions and paths. Most of what those sites need is not BoR-specific. A site that waits for research, targets one faction, changes a pawn on the way home or pays in something other than silver is useful to any content mod. Putting these in Expeditions keeps it ignorant of BoR, and lets BoR (or anyone) ship sites as XML plus small worker classes in their own folder.

Some BoR ideas turned out to be existing sites with a different faction attached: an ally job (`Exp_FriendlyJob`) paid in that ally's goods, a hostile outpost (`Exp_HostileOutpost`) whose loot follows its owner. Those become per-faction data, not new sites.

## Goals
- A site can wait on research, and on any other condition a mod defines.
- A loot entry can wait on research, and can stop dropping once its item has been analysed.
- A site can target settlements of specific faction defs.
- A site can be offered for a real settlement on the world map, and a win can raid it or destroy it.
- A site can run mod code on each returning pawn and once on the outcome.
- A faction can pay in its own goods and add its own loot to named sites.
- A mod can strike items out of a squad's loot before it comes home.
- Four new sites: downed courier, slaver convoy, settlement bounty, dormant mechs.
- Saves from before this keep working, including squads already away.

## Non-goals
- Anything BoR-specific in Expeditions: no BoR def names, no references to its assembly.
- A player-chosen stay length ("stay longer, carry more").
- Penalties for ignoring an offer.
- Settlements in orbit (Odyssey). Same-layer only, as the rest of the mod.

## Anti-goals
- Two offers, or an offer and a squad, on the same settlement at once.
- A destroyed settlement that leaves its faction in a state vanilla wouldn't (not defeated when it had no settlements left, or a settlement removed without a destroyed-settlement marker).
- A hook that throws breaking the resolve: one bad mod worker must not lose a squad.
- An analysed-artifact check that keeps dropping the item because it reads the wrong analysis id.

## Approaches
1. **Hooks in Expeditions, content in the other mod (chosen).** Expeditions grows generic, XML-driven fields and a few abstract worker classes. BoR ships its sites, overrides and workers in its own `Mods/Expeditions` folder. Expeditions stays ignorant of BoR, and BoR follows its usual compat pattern.
2. **A `Compat/BOR` folder in Expeditions.** One repo while building, but Expeditions would depend on BoR internals and follow every BoR rework.
3. **XML only, BoR reached by reflection.** No second assembly, but fragile and hard to debug.

The user picked 1: "Expeditions themselves in BoR, but the research or faction thing stays in the main mod."

## Design

### 1. Gates: when a site can be offered
`ExpeditionSiteDef.gates`: a list of `SiteGate` objects (`<li Class="...">`), all of which must be open for the site to be offered, offered again, or kept on the board. A closed gate withdraws an offer the way `StillFits` does today. It does not touch a squad already away.

```csharp
public abstract class SiteGate
{
    public abstract bool Open();                       // colony-wide
    public virtual string? ClosedReason => null;       // for the debug evaluator
    public virtual IEnumerable<string> ConfigErrors() { yield break; }
}
```

Built-in: `SiteGate_Research` with `research` (a list, all finished) and `anyResearch` (a list, any finished). Mods add their own gates (path chosen, quest open, mechanitor present).

**Offers that can't wait.** Some sites answer something with its own clock: a BoR dark pact lasts 7 days, while a ready-made offer may take 1–3 days to roll and competes with about 45 sites for 5 slots. A site with `immediate` set is offered as soon as its gates open, outside the slots and the timer, the way `SiteLinks.Tick` offers quest sites. There is at most one offer per immediate site at a time. `SiteGate` gets `public virtual int? DeadlineTick => null;`. When a gate returns a tick, the offer expires then, not after `offerLifetimeTicks`. With several gates, the earliest deadline wins.

Gates are checked in `OfferGenerator.ReadyMadeOrder`/`TryMake`, in `SiteLinks.Tick`, in the new settlement-link offers, and in `StillFits`. Prospects and rumours that produce a gated site skip it while it is closed.

### 2. Loot entries that wait, or stop
Two optional fields on `LootEntry`:
- `research` (`ResearchProjectDef`): rolled only once it is finished.
- `untilAnalyzed` (bool): skipped once the thing's analysis is complete. The check reads the thing's `CompProperties_CompAnalyzableUnlockResearch.analysisID` and `Find.AnalysisManager`. The builder confirms the exact vanilla call in the decompile. A thing without that comp logs a config error.

The info lines ("at best") hide entries that wouldn't roll right now.

### 3. Faction defs on a site
`ExpeditionSiteDef.factionDefs` and `enemyFactionDefs` (lists of `FactionDef`). When set, `needsFaction` and `needsEnemy` match only settlements of those defs. `OfferGenerator.Wants` and `NearbyFaction` take the list. Hidden factions stay excluded, as today.

### 4. Settlement links
Today `link` matches only quest `Site`s. A new `settlementLink` block offers a site for a real `Settlement`:

```xml
<settlementLink>
  <workerClass>Expeditions.SettlementLinkWorker</workerClass>
  <need>Hostile</need>                  <!-- FactionNeed of the settlement's owner -->
  <factionDefs><li>...</li></factionDefs>
  <onSuccess>Raid</onSuccess>           <!-- Raid | Destroy -->
  <onPartial>Raid</onPartial>           <!-- Raid | Nothing -->
  <fightPerPoint>1</fightPerPoint>
  <fightMin>200</fightMin>
  <raidCredit>true</raidCredit>
  <affectRelations>false</affectRelations>  <!-- true: a non-hostile owner turns hostile, as when attacked -->
  <retaliation>...</retaliation>        <!-- same RetaliationProps as link -->
</settlementLink>
```

- **Offering.** Settlement-linked sites are ready-made offers: they take an offer slot and follow `commonness`, as normal sites do. A candidate's tile is the settlement's tile, in range. The offer holds the settlement in a new `Opportunity.linkedSettlement` (saved). `Opportunity.faction` is the settlement's owner. One offer per settlement at a time (the anti-goal), and none while a squad is out against it.
- **Patron.** If the def also has `needsFaction`, the patron is found near the settlement as today and pays as today (goodwill, silver). The settlement's owner then goes in `Opportunity.enemy`, so loot and capture already treat it as the holder.
- **Strength.** Settlements have no threat points. Fight is `fightPerPoint` × `StorytellerUtility.DefaultSiteThreatPointsNow()`, at least `fightMin`, through `SiteLinkMath.FightRequirement`.
- **Liveness.** The offer is withdrawn, and an away squad turns back as `SiteLinks.TurnBack` does, if the settlement is destroyed, changes hands, its owner no longer matches `need`, or the player enters it in person (it has a map). On top of that, `public virtual bool StillWanted(Opportunity o) => true;` lets a subclass call the squad back for its own reasons, such as the quest it served having ended. This is the one exception to "a closed gate never touches a squad already away", and it is opt-in per worker.
- **Ending.** `Destroy` does what vanilla does when a settlement is defeated without a map: remove the settlement, place a `DestroyedSettlement`, and run vanilla's faction-defeated check and letter. The builder follows `SettlementDefeatUtility` in the decompile and reuses what it can. `Raid` leaves it standing.
- **Raid credit, relations, retaliation: new code, not reuse.** The post-resolve block in `Resolver.Resolve` runs only for `site.LinkWorker`. `Retaliation.For` reads only `site.link?.retaliation`, and takes the raider from `linkedSite?.Faction ?? opp.faction`, which on a site with a patron is the patron. The settlement path needs its own branch:
  - The raider is the settlement's owner.
  - `retaliation` is read from `settlementLink`.
  - `affectRelations` turns a non-hostile owner hostile on any rolled outcome (Success, Partial or Failure), since the squad attacked either way.
  - Raid credit is given on Success and Partial.
- **Report.** A line for what became of the settlement ("razed" / "raided").

`SettlementLinkWorker` has the same virtuals as `SiteLinkWorker` where they make sense (`CanOffer`, `FightRequirement`, `MakeLoot`, `InfoLines`, `Conclude`), so a mod can subclass it. It does not share a base class with `SiteLinkWorker` unless that falls out naturally.

### 5. Pawn effects and outcome effects
Two lists on `ExpeditionSiteDef`, each of `<li Class="...">` objects:

```csharp
public abstract class PawnEffect
{
    public List<Outcome> outcomes = new() { Outcome.Success, Outcome.Partial, Outcome.Failure };
    // A pawn that is alive, came through the site, and wasn't captured. Seeded Rand is active.
    public abstract void Apply(Pawn p, Expedition e, Outcome outcome, ExpeditionReport report);
}

public abstract class OutcomeEffect
{
    public List<Outcome> outcomes = new() { Outcome.Success };
    // At the site: runs once, after loot and rewards, outside the seeded Rand state (it may call into other mods' state).
    // For what the deed itself earns (goodwill, quest progress), like goodwill is paid today.
    public virtual void Apply(Expedition e, Outcome outcome, ExpeditionReport report) { }
    // At home: runs once in Arrival.Return when the squad is set down on a map (null when they become a caravan),
    // before the report letter is written. For what depends on what actually came home (items, the map's buildings).
    // Not called for a squad lost on the road.
    public virtual void OnArrival(Expedition e, Outcome outcome, Map? map, ExpeditionReport report) { }
}
```

- Pawn effects run in `Resolver.Resolve` after fates, for humanlike pawns whose fate is Unharmed or Injured.
- `OutcomeEffect.Apply` runs after `Reward`, outside `Rand.PushState`, next to the link worker's `Conclude`. `OnArrival` runs in `Arrival.Return`.
- Each call is wrapped in try/catch. A failure logs once per def and the resolve carries on (the anti-goal).
- Effects write to `report.events` (existing) or to a new `report.effectLines` shown after the goodwill lines. The builder picks one and sticks to it.

Built-in effects:
- `OutcomeEffect_GoodwillAll`: `amount`, `includeHostile` (bool), `exceptFactionDefs`. Halved on Partial when Partial is in `outcomes`. This replaces nothing; `goodwillAllFriendly` stays as it is.

### 6. Faction overrides
A new def type, `ExpeditionFactionDef`, at most one per `FactionDef`:

```xml
<Expeditions.ExpeditionFactionDef>
  <defName>...</defName>
  <faction>SomeFactionDef</faction>
  <gates>...</gates>                <!-- SiteGates; while any is closed the whole override is off -->
  <payment>                         <!-- this faction as patron pays silverReward in these, at equal market value -->
    <li>ThingA</li><li>ThingB</li>
  </payment>
  <siteLoot>                        <!-- this faction as holder of the named sites -->
    <li>
      <site>Exp_HostileOutpost</site>
      <replace>false</replace>      <!-- true: instead of the site's table; false: added to it -->
      <loot>...LootEntry list...</loot>
    </li>
  </siteLoot>
  <outcomeEffects>                  <!-- added to the named sites when this faction holds them -->
    <li><site>...</site><effects>...</effects></li>
  </outcomeEffects>
</Expeditions.ExpeditionFactionDef>
```

- **Payment.** The patron is `Opportunity.faction` on a site with `needsFaction`. The silver amount after the Partial cut is converted into the listed things. The value is split evenly across them and rounded down, with any remainder paid in silver. Info lines and the report show the goods.
- **Holder.** The holder is `enemy ?? faction`, the same rule `LootRoller.Roll` uses today.
- **Gates.** While a gate is closed, the faction pays silver and holds the site's own loot, as if there were no override. This keeps tier goods away from a colony that hasn't reached the tier.
- Linked quest sites (`link`) ignore overrides. Their loot comes from the quest.

### 7. Loot filters
`ExpeditionLootFilter` (abstract, registered by subclassing; Expeditions finds subclasses by reflection at startup, the way RimWorld finds `DefOf`s):

```csharp
public abstract class ExpeditionLootFilter
{
    // Remove what this mod forbids. Return a report line, or null.
    public abstract string? Filter(Expedition e, List<Thing> loot);
}
```

It runs in `LootRoller.Roll` before `Fit`, so the haul is weighed without the struck items. It runs again in `Arrival.Return` over the holder's non-pawn contents before they are released, so a rule that changed while the squad walked home still applies. Struck things are destroyed. Expeditions itself ships none.

### 8. Joiners on Partial, and mech joiners
- `JoinerSpec.partialFactor` (default 0): the chance multiplier on a Partial. Today joiners need a full Success and a find. With a factor above 0 they also roll on Partial at `chance × partialFactor`.
- `JoinerSpec.mechKinds` (Biotech): a list of `<li><kind>PawnKindDef</kind><research>optional</research></li>`. When set, the joiner is a mech picked from the entries whose research is finished. The site needs a mechanitor in the squad. Without one the joiner roll is skipped, and the offer says so in its info lines. The mech becomes the player's and is assigned to that mechanitor as overseer. Over bandwidth, vanilla's rules apply: it is uncontrolled, not refused.

### 9. Saving
New saved fields: `Opportunity.linkedSettlement` (reference), and `report.effectLines` if that route is taken. Everything else is defs. Old saves load with no settlement links and no new offers until the board refills.

## New sites
Numbers are starting points for tuning. Challenge entries are (weight, requirement).

| Def | Offered when | Challenges | Loot and rewards | Risk |
| --- | --- | --- | --- | --- |
| `Exp_DownedCourier` | anywhere, commonness 0.6 | Hack (2, 130), Search salvage (1, 100), Endure (0.5, 100) | ComponentSpacer 1~2; Gold 10~30; Silver 100~300 (0.6); one rare item, full success only (the builder picks a sensible vanilla pool: a neurotrainer, a techprint with Royalty, an archotech-tier medical item) | default |
| `Exp_SlaverConvoy` | a road; an ally patron (`needsFaction` Ally) and a hostile enemy (`needsEnemy`) | Fight (2, 250), Stealth (1.5, 100), Endure (0.5, 100) | joiners (chance 0.7, 1~3, Colonist, `partialFactor` 0.5); patron goodwill 4; silverReward 200 as the bounty; the slavers' gear: WeaponsRanged 1~2, Apparel 1~2 | harsh |
| `Exp_SettlementBounty` | a real hostile settlement in range (`settlementLink`, Raid on Success and Partial) and a friendly patron near it | Fight (2, from the settlement), Stealth (1, 100), Haul (1, 150) | silverReward 300; patron goodwill 5; the holder's stores: WeaponsRanged 1~3, Apparel 1~3, MedicineIndustrial 2~6, Silver 100~300 | harsh |
| `Exp_DormantMechs` (Biotech) | the colony has a mechanitor, hilly or not | Hack (2, 130), Fight (1, 150), Search salvage (1, 100) | mech joiner (chance 0.5 on Success, 1, `mechKinds`: Lifter, Constructoid, Cleansweeper, Agrihand, Paramedic); Steel 50~150; Plasteel 10~40 (0.5); ComponentIndustrial 2~5 | harsh, no captors |

How they differ from what is there: the courier is hacking for one prize, where the crashed ship is salvage. The convoy is on a road with a patron paying, where the holding camp is a fixed camp tied to a captive. The bounty is a real settlement, where the enemy camp is generated. Dormant mechs bring a mech home, where the mech cluster gets stripped.

## What is left to the builder
- Where the gate checks go inside the offer code, as long as every path that makes or keeps an offer goes through them.
- Whether `SettlementLinkWorker` shares a base with `SiteLinkWorker`.
- `report.events` versus a new `effectLines`.
- The rare-item pool for the courier.
- Exact vanilla calls for analysis completion and settlement defeat, from the decompile.
- Tests: the pure parts (the payment split, the gate combination, the joiner chance on Partial, the fight requirement for settlements) go in `Source/Logic` with tests in `Source/Tests`, as the mod does today.

## Order of work (suggested)
1. Gates and loot-entry conditions.
2. Faction defs on sites, then faction overrides.
3. Pawn and outcome effects, with the built-in goodwill effect.
4. Loot filters.
5. Settlement links, then the settlement bounty.
6. Joiner changes, then the slaver convoy and dormant mechs.
7. The downed courier.
8. Player site (`site/`) entries for the new sites and fields.
