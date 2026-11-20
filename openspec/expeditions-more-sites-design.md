# Expeditions: More Sites

Adds 22 site defs and the mechanics they need. It extends `expeditions-mod-design.md` and changes nothing in it unless a section below says so.

## Intent
The current sites mostly reward Fight, Haul and Search, and pay out in loot. The new sites give the player more reasons to send a squad: goodwill, training, recruits, rare implants, and jobs that a cook, a doctor, a farmer or a teacher can do better than a soldier.

## Goals
- Most squad strengths have at least one site where they matter most: farming, building, social, animals, medicine and intellectual, as well as fighting.
- Neutral factions pay goodwill too, as well as allies.
- A few hard sites carry one rare item (a mechlink or a psylink neuroformer) that a colony can't easily get any other way.
- Everything stays in XML where possible. Each new mechanic is one def field with a default that leaves the existing sites unchanged.

## Non-goals
- Real maps, world objects, or anything placed on the vanilla world map (same as the base design).
- Quest chains, or follow-up offers that depend on an earlier result.
- Diseases picked up on site (see *Later*).
- Changing the existing nine sites, apart from the new defaults.

## Mechanics
Each mechanic is listed with the field it adds and the code it touches. The defaults keep current behavior.

### M1. Friendly factions
`FactionNeed.Friendly`: a non-hidden faction that is not hostile to the player (neutral or ally) with a settlement near the tile. Add it to `OfferGenerator.Wants`. Village sites use it.

### M2. A second faction: the enemy
`needsEnemy` (bool, default false): the tile also needs a hostile faction's settlement within 12 tiles (the patron stays within `FactionSiteRadius`, 5). It works alongside `needsFaction`: the patron is `needsFaction`, and the enemy is who the squad fights.
- `Opportunity.enemy` (saved): the hostile faction found.
- `Resolver.Capture`: prefer `opp.enemy` as the captor, then `opp.faction` if it is hostile, then any enemy faction.
- `LootRoller`: the tech limit comes from `opp.enemy` when it is set (the loot is theirs), otherwise from `opp.faction`.
- `TryFindTile` keeps sampling near patron settlements and rejects tiles with no enemy nearby.
- An offer is withdrawn from the board when its patron no longer qualifies or its enemy is no longer hostile. At resolution, a patron that turned hostile pays no goodwill, and a defeated enemy can't take captives.

### M3. Goodwill with every friendly faction
`goodwillAllFriendly` (int, default 0): on Success, every non-hidden, non-defeated, non-player faction that is not hostile gains this much goodwill. On Partial it gains half, rounded up (as does `goodwillReward`). The patron gets this on top of `goodwillReward`. These changes send no per-faction message; the report carries one line for all of them.

### M4. More training: `xpFactor` and skill inputs
Survivors already gain `xpPerChallengeWeight × weight` XP in each challenge's skills (`Resolver.ApplyFate`).
- `xpFactor` on the site (float, default 1) multiplies that XP. Tournaments and teaching raise it.
- `ChallengeStat.skill` (a SkillDef, used instead of `stat`): the pawn's skill level counts as the value. A totally disabled skill counts as 0. The explanation reads "Ann: Artistic 12 (burning passion)". This is needed because vanilla has no stat that tracks Artistic, Crafting or Cooking skill directly. `Contribution` then holds a label instead of a `StatDef`, and `ConfigErrors` checks that each entry has exactly one of `stat` or `skill`.

### M5. New challenges
| defName | Label | Combine | Inputs (reference) | Skills (XP) |
| --- | --- | --- | --- | --- |
| `Exp_Farm` | farm | Sum | PlantWorkSpeed (1), PlantHarvestYield (1) | Plants |
| `Exp_Build` | build | Sum | ConstructionSpeed (1), ConstructSuccessChance (1) | Construction |
| `Exp_Hack` | hack | Max | HackingSpeed (1), ResearchSpeed (1, weight 0.5) | Intellectual |
| `Exp_Teach` | teach | Average | SocialImpact (1), skill Shooting (8), skill Melee (8) | Social, Shooting, Melee |
| `Exp_Tame` | tame | Max | TameAnimalChance (0.28, about Animals 8) | Animals |
| `Exp_SearchDig` | search (dig) | Max | MiningSpeed (1), ResearchSpeed (1) | Mining, Intellectual |
| `Exp_Contest_<Skill>` | contest | Max | see Tournaments | that skill |

Every stat named here exists in Core (`Stats_Pawns_WorkGeneral.xml`, `Stats_Pawns_Social.xml`).

### M6. Loot that needs a full success
`LootEntry.successOnly` (bool, default false): the entry is rolled only on Success. Without this, `Scale` rounds a single item up to 1 on Partial, so a half-failed raid would still bring home the mechlink.

### M7. Pawns as rewards
`joiners` on the site, optional:
```xml
<joiners><chance>0.6</chance><count>1~2</count><kind>Colonist</kind></joiners>
```
On Success with Find, roll `chance`. If it passes, generate `count` pawns of `kind` for the player's faction (ideoligion and xenotype as vanilla does for that kind) and add them to the expedition's holder.

`tamedAnimals` on the site, optional:
```xml
<tamedAnimals><chance>0.8</chance><count>1~3</count><maxWildness>0.85</maxWildness></tamedAnimals>
```
Animals are picked from the tile biome's wild animals (`AllWildAnimals`), filtered by wildness and weighted by commonness. They are tamed to the player and placed in the holder.

For both:
- They come home with the squad, need no hauling room, and eat nothing on the way back (they are frozen like the rest of the holder).
- Each gets its own "joined the colony" row in the report (`PawnFate.Joined` in `report.pawns`).
- They are generated inside the resolution's `Rand.PushState(seed)`, so a reload gives the same result.

### M8. Commonness
`commonness` on the site (float, default 1). `OfferGenerator` picks site candidates weighted by commonness instead of uniformly. Without it, the six tournament defs would take six of every ~30 offers.

### M9. Season
`growingSeason` (bool, default false): the tile's seasonal average temperature must lie between 6 and 42 °C, the band where crops grow at full speed. Vanilla has no tile-based growing-season check (only a map one), and the season average keeps a cold night from hiding a harvest. Used by the harvest site.

### M10. The report and the label
- The report lists goodwill changes: the patron's, and "+N with all friendly factions" in one line when M3 applies.
- For sites with a faction (a patron, or the hostile outpost's owner), `Opportunity.Label` adds it: "help with harvest (Ashwood Union)". For M2 sites it adds the enemy as well: "enemy camp (Red Knives, for Ashwood Union)".
- Sites with nothing to find (no loot, joiners, animals, target or captive) skip the "found / found nothing" line, and jobs where nothing is searched for have `findChanceBase` 1. `successText` on a site adds a line to the report on a full Success (used by rescue for an ally).

### M11. DLC and mod gating
Sites that need a DLC or mod put `MayRequire` on the def element itself (`<Expeditions.ExpeditionSiteDef MayRequire="Ludeon.RimWorld.Biotech">`), so the def isn't loaded and no missing reference is reported. Single loot lines use `MayRequire` on their `li`.

## Sites
Requirement levels follow the existing scale: 100 is one decent pawn on a Max, Min or Average challenge. Sum challenges add up, so 300 is about three decent fighters. **Low**, **medium** and **hard** refer to these numbers and the risk profile. Every number is a starting point for tuning.

Risk profiles used below:
- **default:** the `RiskProfile` defaults.
- **harsh:** as the hostile outpost has it (killed 0.4 and captured 0.4 on Failure).
- **gentle:** success `unharmed 0.95, injured 0.05`; partial `0.8 / 0.2`; failure `0.5 / 0.45 / killed 0.05`. Nobody is captured.
- **no captors:** any profile with `captured` moved into `killed`, for sites with mechs or animals as the threat. Mechs and insects don't take prisoners.

### Goodwill (allies)
| defName | Label | Needs | Challenges (weight, requirement) | Reward | Risk, time, squad |
| --- | --- | --- | --- | --- | --- |
| `Exp_RescueKin` | rescue for an ally | Ally + enemy (M2) | Stealth (1.5, 100), Fight (1.5, 250) | goodwill 12 | harsh; 0.5 d; 1-4 |
| `Exp_EnemyCamp` | enemy camp | Ally + enemy (M2) | Fight (2, 350), Stealth (0.5, 100), Haul (0.5, 150) | goodwill 10, silver 200; WeaponsRanged 1~2, WeaponsMelee 1 (0.5), Apparel 1~2 (enemy tech) | harsh; 0.5 d; 3-6 |
| `Exp_Garrison` | relieve a garrison | Ally | Fight (1.5, 250), Endure (2, 120) | goodwill 10, silver 300 | default; 3 d; 3-6 |

Rescue for an ally: the description names the patron ("one of theirs"). No pawn comes home; on Success the report's `successText` says they were returned. The garrison's long `daysOnSite` makes food its main cost.

### Learning (tournaments)
An abstract `Exp_TournamentBase`: needs a Friendly faction, `commonness 0.2`, `xpFactor 3`, squad 1-3, gentle risk, 1 day, silver prize 150 (halved on Partial), goodwill 2. Each tournament has one challenge, `Exp_Contest_<Skill>` (weight 1, requirement 120, so a skill-12 champion is about even).

| defName | Label | Contest inputs (reference) |
| --- | --- | --- |
| `Exp_Tournament_Shooting` | shooting contest | ShootingAccuracyPawn (0.9), skill Shooting (12) |
| `Exp_Tournament_Melee` | melee bouts | MeleeHitChance (0.9), MeleeDPS (4), skill Melee (12). Risk: injuries common (0.3 / 0.6 / 0.75 by outcome), death only on Failure (0.05), no capture |
| `Exp_Tournament_Cooking` | cook-off | skill Cooking (12) |
| `Exp_Tournament_Artistic` | art fair | skill Artistic (12) |
| `Exp_Tournament_Crafting` | crafting fair | skill Crafting (12) |
| `Exp_Tournament_Debate` | debate | SocialImpact (1), NegotiationAbility (1), skill Social (12) |

### Goodwill with every friendly faction, plus learning
| defName | Label | Needs | Challenges | Reward | Risk, time, squad |
| --- | --- | --- | --- | --- | --- |
| `Exp_DefendVillage` | protect a village | Friendly | Fight (2, 250), Endure (1, 100) | goodwill 5, all friendly 2, silver 150 | default; 1 d; 2-6 |
| `Exp_TeachVillage` | teach a village | Friendly | Teach (2, 100), Endure (0.5, 100) | goodwill 3, all friendly 2, `xpFactor 1.5` | gentle; 2 d; 1-4 |
| `Exp_PlagueVillage` | plague village | Friendly | Endure (2, 150), Haul (0.5, 100) | goodwill 6, all friendly 1 | gentle; 2 d; 1-4 |

### Rewards
| defName | Label | Needs | Challenges | Loot | Tier, risk, time |
| --- | --- | --- | --- | --- | --- |
| `Exp_Harvest` | help with harvest | Friendly, growing season (M9) | Farm (2, 200), Haul (1, 150), Endure (0.5, 100) | PlantFoodRaw 80~200; goodwill 2 | low; gentle; 1 d |
| `Exp_Rebuild` | help rebuild | Friendly | Build (2, 200), Haul (1, 150), Endure (0.5, 100) | silver 250; goodwill 3 | low; gentle; 1 d |
| `Exp_MechanitorComplex` | ancient mechanitor complex | Biotech (M11); LargeHills, Mountainous | Hack (2, 130), Fight (1.5, 350), Endure (1, 100) | Mechlink 1 (successOnly), ComponentSpacer 1~3, Plasteel 30~80 | hard; harsh, no captors; 1 d; `commonness 0.3`, `findChanceBase 0.7` |
| `Exp_ImperialTemple` | ruined imperial temple | Royalty (M11) | Stealth (2, 120), Hack (1, 120), Fight (1, 300) | PsychicAmplifier 1 (successOnly), Gold 10~40, Silver 100~300 | hard; harsh; 1 d; `commonness 0.3`, `findChanceBase 0.7` |
| `Exp_MilitaryDump` | military dump | none | Fight (1.5, 200), Stealth (1, 100), Haul (1.5, 200), Search salvage (1, 100) | WeaponsRanged 1~3, WeaponsMelee 1 (0.5), Chemfuel 30~80, ComponentIndustrial 1~3 (0.5) | medium; default; 0.5 d. Serves Weapons |
| `Exp_AmmoDump` | ammunition dump | Combat Extended (M11) | Haul (2, 250), Fight (1, 200), Search salvage (1, 100) | AmmoRifles 100~400, AmmoPistols 100~300, AmmoShotguns 50~200 (0.6), AmmoHighCaliber 30~100 (0.5), AmmoShells 5~15 (0.3), AmmoGrenades 5~15 (0.3) | medium; default; 0.5 d. Serves that category |
| `Exp_ColonyRuins` | ruins of a colony | none | Search salvage (2, 100), Endure (1, 100), Haul (1, 150) | Steel 30~80, Textiles 20~60, MedicineIndustrial 1~4 (0.5), Apparel 1~2 (0.6), Manufactured 1~3 (0.5), Foods 10~40 (0.5) | low; gentle; 1 d. Serves ResourcesRaw, Manufactured |

The psylink neuroformer ThingDef is `PsychicAmplifier`. It is defined in Core data, but psylinks only matter with Royalty, so the site is gated on Royalty. The mechlink ThingDef is `Mechlink` (Biotech).

### Other sites
| defName | Label | Needs | Challenges | Loot or reward | Tier, risk, time |
| --- | --- | --- | --- | --- | --- |
| `Exp_Shipwreck` | beached shipwreck | coast (`needsCoast`, no site uses it yet) | Search salvage (2, 100), Haul (1.5, 200), Endure (0.5, 100) | Textiles 30~100, Steel 30~80, ComponentIndustrial 1~4, Silver 50~200 | low; default; 1 d |
| `Exp_InsectHive` | insect hive | LargeHills, Mountainous | Fight (2, 300), Endure (1, 100) | InsectJelly 40~120 (not offered to prospecting, so a search for wool or eggs never lands here) | medium; default, no captors; 0.5 d |
| `Exp_LegendaryBeast` | legendary beast | biomes where thrumbos roam | Search game (1.5, 100), Fight (2, 300) | ThrumboHorn 1 (successOnly), Leather_Thrumbo 40~80, Meat_Thrumbo 100~200 | hard; harsh, no captors; 1 d; `commonness 0.4` |
| `Exp_Escort` | caravan escort | Friendly, road | Fight (1.5, 200), Endure (1, 100) | silver 300, goodwill 4 | medium; default; 2 d |
| `Exp_MechCluster` | downed mech cluster | none | Fight (2, 350), Hack (1, 100), Haul (1, 150) | Plasteel 30~80, Steel 50~150, ComponentSpacer 1~2 (0.4) | hard; harsh, no captors; 0.5 d |
| `Exp_Dig` | archaeology dig | Desert, ExtremeDesert, AridShrubland | Search dig (2, 100), Endure (1, 100), Haul (0.5, 100) | Artifacts 1 (successOnly, 0.6), Gold 5~20, Silver 50~150 | medium; gentle; 2 d. Serves Artifacts |
| `Exp_CryptoVault` | cryptosleep vault | none | Hack (2, 100), Endure (1, 100), Fight (0.5, 150) | `joiners` 0.6 × 1~2; Apparel 1~2 (0.5), MedicineIndustrial 2~5 | medium; default; 1 d |
| `Exp_TamingGrounds` | taming grounds | any biome with wild animals | Tame (2, 100), Endure (1, 100) | `tamedAnimals` 0.8 × 1~3 | low; gentle, no captors; 1 d |

## Order of work
1. **XML only:** military dump, ruins of a colony, garrison, shipwreck, insect hive, mech cluster. The only code needed is moving `captured` into `killed` in the risk profiles.
2. **Small fields:** M1, M6, M8, M10, M11. Unlocks harvest (without the season check), rebuild, mechanitor complex, imperial temple, ammo dump, escort, legendary beast, plague village.
3. **Factions and goodwill:** M2, M3. Unlocks rescue for an ally, enemy camp, protect a village.
4. **Training:** M4, M5. Unlocks tournaments, teach a village, dig, the Hack and Tame challenges.
5. **Pawn rewards and season:** M7, M9. Unlocks cryptosleep vault, taming grounds and the harvest season check.

Logic tests cover weighted site picking (M8, `WeightedOrder`). The rest is checked in game: the dev-mode action **Spawn offer of...** creates an offer for any chosen site, or logs why it fits no tile in range.

## Checked while building
- `Meat_Thrumbo` is generated at load, and Core XML already refers to it, so it resolves.
- `InsectJelly` is in `AnimalProductRaw`.
- `TameAnimalChance` is 0.04 + 0.03 per Animals level, so it already is the skill. `Exp_Tame` reads only the stat.
- Taming grounds only appear where the biome has wild animals under the wildness limit.
- Combat Extended files `Ammo` under `Weapons`, so a CE game could roll 1-3 rounds of ammo as a "weapon". Weapon loot therefore draws from `WeaponsRanged` and `WeaponsMelee`, never `Weapons`, and the crashed ship and hostile outpost were changed to match.
- Combat Extended: the packageId is `CETeam.CombatExtended`, and `Ammo` (a child of `Weapons`) holds AmmoRifles, AmmoPistols, AmmoShotguns, AmmoHighCaliber, AmmoShells, AmmoGrenades, AmmoRockets, AmmoMissiles, AmmoAdvanced and others. The ammo dump draws small arms in bulk and heavier rounds in small lots, and leaves out AmmoAdvanced (spacer charged ammo), rockets and missiles.

## Later (not in this spec)
- Illness picked up on site (plague village, swamps) as a fate alongside injuries.
- Follow-ups: a rescued ally offering a job, a ruin hinting at a vault.
- BOR site packs, as in the base design.
