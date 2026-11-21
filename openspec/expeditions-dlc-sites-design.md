# Expeditions: DLC world sites

Royalty, Biotech, Ideology and Odyssey quest sites on the world map show up on the expedition board, the same way Ideology work sites do now. A squad can do the site for you instead of a caravan. This extends `expeditions-worksites-design.md` and generalises its link to vanilla sites. Work sites keep their current rules.

## Intent
These sites are quests that send a caravan to a map to fight for a prize, or to destroy something that is hurting the colony. The player gets the same choice as with work sites: go in person, or send a squad and read the report. Unlike work sites there is no cut. A squad that wins brings home everything, and the risk is in the roll.

## Outcome rules
For every site in this doc:
- **Success:** the squad gets the whole prize (still cut to what it can carry), or the harmful thing is destroyed and its effect on the colony ends.
- **Partial:** the same as Success. On top of that, whoever held the site raids the colony in response (see *Retaliation*).
- **Failure:** no loot. A harmful site stays where it is and can be tried again. A prize site is abandoned: its quest fails and the site is removed.

Work sites are unchanged: 60% of the stash on Success, 30% on Partial, nothing on Failure, the site is removed on every outcome, and no retaliation raid.

## Goals
- Every site listed below appears on the board when it is in range, as its own offer tied to that site.
- Squads resolve these sites under the mod's usual challenges, risk and carry limit, with the outcome rules above.
- Fight scales with the site's own threat points.
- What each kind of site needs lives in XML. Code exists only where vanilla needs real calls.
- Existing saves, including squads already out on work sites, keep working.

## Non-goals
- Orbital sites: the mechhive, orbital platforms, satellites, orbital wrecks. Squads travel on the surface only.
- Gravship wreckage. It is part of Odyssey's opening story.
- Core sites (bandit camps, item stashes, other ancient complex quests). They could come later as new defs.
- Generating the site's real map to count its loot. Loot comes from the quest, from vanilla's loot makers, or from the mod's tables.
- Patching vanilla quests.

## Anti-goals
- A site that can be done twice, by two squads or by a squad and a caravan.
- Vanilla quest signals fired without a map (some log errors, some end the quest wrongly).
- Prisoners taken by factions that can't hold anyone: mechs, insects, the ancients.
- A retaliation raid lost on save and load, or fired at a map that is gone.

## Sites
"Threat" means the Fight requirement comes from the site's threat points (see *Difficulty*). Challenge entries are (weight, requirement). Risk profiles are the mod's existing families. Every number is a starting point for tuning, and all of these are my calls unless they say otherwise.

### Harmful
| Def | Matches (quest root) | Challenges | Loot | Risk | Raid after Partial |
| --- | --- | --- | --- | --- | --- |
| `Exp_ProblemCauser` (Royalty) | `ProblemCauser` | Fight (2, threat), Stealth (1, 100), Hack (1, 120) | none: the reward is that the effect ends | harsh. No captors when the site's faction is mechanoids. | the site's faction: pirates or mechanoids |

The quest picks a pirate outpost or a mech cluster, plus one condition causer (sun blocker, smoke or toxic spewer, psychic droner or suppressor, weather controller, climate adjuster, EMI dynamo). One def covers all of them. The label is the vanilla site label.

### Prize
| Def | Matches (quest root) | Challenges | Loot | Risk | Raid after Partial |
| --- | --- | --- | --- | --- | --- |
| `Exp_MechanitorComplexSite` (Biotech) | `OpportunitySite_AncientComplex_Mechanitor` | Hack (2, 130), Fight (1.5, threat), Search salvage (1, 100) | Mechlink 1; `MapGen_AncientComplexRoomLoot_Better` rolled 3 times | harsh, no captors | mechanoids |
| `Exp_AncientComplexSite` (Ideology) | `OpportunitySite_AncientComplex`, `OpportunitySite_AncientComplex_Giver` | as above | `MapGen_AncientComplexRoomLoot_Better` rolled 3 times, plus the quest's item rewards (Giver) | harsh, no captors | mechanoids |
| `Exp_GravcoreSite` (Odyssey) | `Gravcore_AbandonedSettlement`, `Gravcore_MechanoidRelay`, `Gravcore_AncientReactor`, `Gravcore_AncientStockpile`, `Gravcore_CrashedMechanoidPlatform`, `Gravcore_FrozenTerraformer` | Fight (2, threat), Endure (1, 100), Haul (1, 150) | the quest's listed reward defs (Gravcore 1, GravlitePanel 10), plus Steel 50~150, ComponentIndustrial 2~5, Plasteel 10~40 (0.5) | harsh, no captors | mechanoids |
| `Exp_GravcoreCaves` (Odyssey) | `Gravcore_InsectLair` | Fight (2, threat), Endure (1.5, 120) | as the gravcore site, plus InsectJelly 40~120 | harsh, no captors | insects |
| `Exp_AncientWarehouse` (Odyssey) | `Opportunity_AncientStructureWarehouse` | Search salvage (2, 100), Endure (1, 100), Fight (1, threat), Haul (1, 150) | Silver 200~600, Gold 10~40, ComponentIndustrial 3~8, MedicineIndustrial 4~10, Textiles 60~150, Apparel 1~3 (0.5) | default, no captors | mechanoids |
| `Exp_AncientRefinery` (Odyssey) | `Opportunity_AncientStructureChemfuelRefinery` | Search salvage (1, 100), Haul (2, 250), Fight (1, threat) | Chemfuel 300~700, ComponentIndustrial 2~5, Steel 50~150 | default, no captors | mechanoids |
| `Exp_AncientGarrison` (Odyssey) | `Opportunity_AncientStructureGarrison` | Fight (2, threat), Search salvage (1, 100), Endure (1, 100) | WeaponsRanged 2~4, WeaponsMelee 1~2 (0.6), Apparel 2~4, MedicineIndustrial 2~6, Silver 100~300 | harsh, no captors | mechanoids |
| `Exp_AncientLaunchSite` (Odyssey) | `Opportunity_AncientStructureLaunchSite` | Search salvage (1.5, 100), Haul (1.5, 200), Fight (1, threat) | TransportPod 2~4, Chemfuel 100~250, Steel 80~200 | default, no captors | mechanoids |
| `Exp_InfestedSettlement` (Odyssey) | `Opportunity_AncientInfestedSettlement` | Fight (2, threat), Endure (1, 100), Search salvage (1, 100) | InsectJelly 50~150, Steel 50~150, Silver 100~300, ComponentIndustrial 1~4, Manufactured 1~3 (0.5) | harsh, no captors | insects |
| `Exp_AncientMercenaries` (Odyssey) | `OpportunitySite_AncientMercenaries` | Fight (2, threat), Stealth (1.5, 100), Haul (1, 150) | WeaponsRanged 2~4, Apparel 2~4, Silver 200~500, MedicineIndustrial 2~6 | harsh (captors only if the site faction can hold prisoners) | pirates |
| `Exp_AlphaThrumbo` (Odyssey) | `OpportunitySite_AlphaThrumbo_Giver` | Search game (1.5, 100), Fight (2, threat), Haul (1, 200) | the alpha thrumbo's horn, meat and leather (defs and counts read from its race in the Odyssey data), plus the quest's item rewards | harsh, no captors | manhunter pack of the tile's animals |

Each def is gated with `MayRequire` on its DLC. A def whose quest root isn't loaded never matches.

## Approaches
1. **One generic link, all in XML**, driven by flags. The least code, but ending conditions, running loot makers and reading quest rewards all turn into flags inside one class.
2. **One C# handler per family.** Flexible, but scanning, liveness, turning back, retaliation and the UI get copied six times, and the numbers end up in code.
3. **XML data plus a small worker per family (chosen).** This follows RimWorld's `workerClass` habit. A base worker does most of the work from XML flags. Subclasses exist only for work sites, problem causers, complexes and quest-listed rewards.

The user picked 3, after a review of the code and the vanilla decompile. The review also added the two changes described below: matching on the owning quest, and keeping most behaviour in the base worker.

## Design

### The link block
`ExpeditionSiteDef.link` (new class `SiteLinkProps`, null for the mod's own sites):
- `workerClass` (default `SiteLinkWorker`)
- `questRoots` (`List<QuestScriptDef>`), with optional `siteParts` (`List<SitePartDef>`) to split one quest family by part
- `failureKeepsSite` (bool): true for harmful sites
- `affectRelations` (bool): true for work sites only
- `raidCredit` (bool): whether Success or Partial calls `IdeoUtility.Notify_PlayerRaidedSomeone`. True for work sites, problem causers and mercenaries.
- `fightPerThreatPoint` (float, default 1) and `fightMin` (default 100)
- `rewardCounts`: a list of def/count pairs, the counts for reward defs the quest lists without a count
- `lootMaker` (`ThingSetMakerDef`) and `lootMakerRolls` (int)
- `retaliation`: `source` (SiteFaction, Mechanoids, Insects, Pirates or Manhunters), `pointsFactor` (0.5) and `delayTicks` (30000)

The mod's existing `loot` list on the site def stays the source for fixed tables. `FromWorldOnly` becomes `rescueOnly || link != null`. `worksiteOnly` is removed, and `Exp_WorkSite` gets a `link` block with the work site worker instead.

### Matching
A site's quest is the ongoing quest with a `QuestPart_SpawnWorldObject` whose `worldObject` is the site. This is how vanilla finds it in `Site.CanBeSettled`. A def matches when the quest's `root` is in `questRoots`, and, when `siteParts` is set, any of the site's parts is listed. Site parts alone aren't enough: several quests share `AncientComplex`, `Outpost` is used everywhere, and Odyssey's tags overlap.

### Classes
- `Source/Defs/SiteLinkProps.cs`: the block above, with `ConfigErrors`.
- `Source/World/SiteLinkWorker.cs`: the base worker, with these virtuals:
  - `Matches(Site, Quest?)`
  - `FightRequirement(Site)`: threat points × factor, at least `fightMin`
  - `Preview(Opportunity)`: the "At best" line
  - `MakeLoot(Expedition, Outcome)`: the `loot` table, quest-listed rewards with `rewardCounts`, `lootMaker` rolls, and the quest's `Reward_Items`
  - `InfoLines(Opportunity)`: extra rows for the offer panel
  - `Conclude(Site, Quest?, Outcome, report, raiders)`: the consequences
- Subclasses:
  - `SiteLinkWorker_Worksite`: today's `WorksiteLink` stash share, guards, `AffectRelationsOnAttacked` and hostility warning
  - `SiteLinkWorker_ProblemCauser`: ends the conditions (see *Consequences*)
  - `SiteLinkWorker_Complex`: adds the `lootMaker` rolls
  - `SiteLinkWorker_QuestReward`: reads the reward defs from the quest's `QuestPart_Choice`
- `Source/World/SiteLinks.cs`: what `WorksiteLink` does today, for all link defs:
  - one scan of `Find.WorldObjects.Sites`
  - `QuestOf(site)`
  - `Alive`: also requires the quest to be ongoing
  - `Linked`, `Lost` and `TurnBack`
  - the deadline from `QuestPart_WorldObjectTimeout`. Sites without one get no expiry.
- `Source/World/Retaliation.cs`: `PendingRaid : IExposable` and `Fire`.
- `Resolver`, `LootRoller` and `Opportunity.Requirement` each have one branch on `opp.site.link`, and that branch calls the worker.

### Difficulty
Fight's requirement is `max(fightMin, threatPoints × fightPerThreatPoint)`, with the threat points from the site's parts (`parms.threatPoints`, falling back to the site's `ActualThreatPoints`). Work sites keep guards × 90 through their worker. The offer panel shows the threat points or guards alongside the challenge bars, as now.

### Loot and preview
- **Quest-listed rewards:** `Reward_DefinedThingDef` only displays a reward in vanilla; the item itself is on the generated map. So the mod makes it, with the count from `rewardCounts`, or 1 if none is listed. The preview is exact.
- **`Reward_Items`** in the quest's first choice (the Giver quests): those items go into the haul. Goodwill rewards apply as the faction's goodwill. The preview lists them.
- **Mod tables:** the existing `LootEntry` list. The preview shows the max count of each entry with chance 1.
- **Loot makers:** generated at resolution only. The preview says "plus ancient complex loot", because a loot maker depends on world state and can't be reliably previewed.
- **Work sites:** unchanged. `Stash()` stays work-site only (other sites keep other things in `parts[0].things`, such as the relay's stabilizers).

Everything is then cut to carry capacity by `Fit`, as now.

### Consequences
`Conclude` runs after `Rand.PopState`, once all loot is rolled.
- **Raid credit:** when `raidCredit` is set and the outcome isn't Failure, call `IdeoUtility.Notify_PlayerRaidedSomeone(surviving humanlike squad)`.
- **Relations:** work sites only, as now.
- **Quest:** `quest.End(Success)` on Success or Partial, and `quest.End(Fail)` on Failure unless `failureKeepsSite`. Always `sendLetter: false`. Never fire the quest's own signals: `conditionCauser.Destroyed` runs a raider notify that logs an error with no map, and the insect lair quest succeeds on `MapGenerated`.
- **Site:** `site.Destroy()` after ending the quest (`quest.End` doesn't remove a spawned site), unless Failure with `failureKeepsSite`.
- **Problem causers, on Success or Partial:**
  1. End the quest.
  2. `End()` every condition in the causer's `CompCauseGameCondition.CausedConditions`.
  3. Destroy the causer thing with `DestroyMode.QuestLogic`, which sends no signal.
  4. Destroy the site.
- **Links:** `opp.linkedSite` is cleared after `Conclude`, and so is `opp.faction` when that faction is temporary. Hidden factions are never stored in `opp.faction`, and `Capture` also skips hidden factions.

### Retaliation
- On Partial at a site with a `retaliation` block, the expedition stores a `PendingRaid` (saved with the expedition).
  - It holds the source, a faction reference (only for a non-temporary, non-hidden SiteFaction), the animal kind for manhunters and the points factor.
- When the squad gets home, it moves to `ExpeditionsGame.pendingRaids` with `fireTick = now + delayTicks`.
  - This happens on every path in `Arrival.Return`, including `BecomeCaravan`.
  - A squad that dies away takes its raid with it, since nobody came home.
- `Tick250` fires due raids:
  - **Map:** the origin map if it's still a player home, else any player home. With no home map it retries daily for 3 days, then drops the raid.
  - **Points:** `StorytellerUtility.DefaultThreatPointsNow(map) × 0.5`, floored at the faction's minimum for a raid group.
  - **Who comes:**
    - Mechanoids: `RaidEnemy` with `Faction.OfMechanoids`.
    - Pirates and SiteFaction: `RaidEnemy` with that faction. If it's gone or no longer hostile, use `RandomEnemyFaction`, and if there's none, skip.
    - Insects: vanilla's infestation incident, unless the insect faction has a raid group maker.
    - Manhunters: `ManhunterPack` with `parms.pawnKind` set to the stored animal kind, or the map biome's default.
  - All of this runs outside any pushed `Rand` state.
- The report says a raid is coming: "Expect {faction} to answer within a day."

### Turning back and deadlines
These work as work sites do now, for every link def:
- the squad turns back when the site isn't alive (destroyed, has a map, or its quest is no longer ongoing)
- a squad still boarding is called off
- food is cut to the days away
- a launch is refused when the squad would arrive after the deadline

After a Failure at a harmful site, the site is still alive, so a new offer for it appears once the expedition is over.

### Save compatibility
- `Opportunity.worksite` is renamed in code to `linkedSite`, but keeps the save label `"worksite"`.
- `Exp_WorkSite` keeps its defName.
- New fields default to null or empty.
- Squads already out on work sites resolve through the work site worker with no migration.

## Left to the builder
- **Alpha thrumbo loot:** the race's butcher products and horn def. Read them from the Odyssey thing defs and confirm them in game.
- **`rewardCounts` for GravlitePanel:** confirm 10 against what the vanilla map holds.
- **Who owns the spawn:** check that `OpportunitySite_AlphaThrumbo_Giver` and `OpportunitySite_AncientComplex_Giver` are the quests holding the `QuestPart_SpawnWorldObject`, not a parent or child quest.
- **Insects:** whether they can run `RaidEnemy`, by checking their pawn group makers. Otherwise use the infestation incident.
- **Raid credit:** whether vanilla calls `Notify_PlayerRaidedSomeone` for complexes, gravcore sites and structures. Set `raidCredit` to match.
- **Report text:** wording for the retaliation line, and the English keys.
- **Player site:** a section per DLC on `site/content/finds.md`, with `build.py` page groups for the new defs.

## Testing
- **Logic** (`Source/Logic`): the Fight requirement from threat points, and retaliation points with the floor and factor.
- **In game**, with the dev quest tool, for one site per family:
  - the offer appears once, with the right label, preview and deadline
  - Success brings the loot, removes the site and ends the quest
  - Partial does the same, then a raid arrives about half a day after the squad gets home, and it survives a save and reload in between
  - Failure abandons a prize site but leaves a harmful site, which then comes back on the board
  - a problem causer's condition ends within a fifth of a day of a Success
  - no letters or errors from quest signals
  - with each DLC off, nothing changes and no errors appear
