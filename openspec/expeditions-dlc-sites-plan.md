# DLC World Sites Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Royalty, Biotech, Ideology and Odyssey quest sites on the surface appear on the board. A squad resolves one with full loot on Success, loot plus a retaliation raid on Partial, and Failure that abandons a prize site or leaves a harmful one.

**Architecture:** Work sites already link to the board. That link is generalised as follows:
- Each linked `ExpeditionSiteDef` carries a `link` block (`SiteLinkProps`) and a worker (`SiteLinkWorker` plus four subclasses).
- `SiteLinks` scans, matches a site to a def by its owning quest, and handles liveness, turning back and deadlines.
- `Resolver` and `LootRoller` call the worker through a single branch.
- Retaliation raids are saved in the mod's own list and fired from `Tick250`.

**Tech Stack:** C# (net10.0), RimWorld 1.7 Assembly-CSharp, xunit for `Source/Logic`.

**Spec:** `openspec/expeditions-dlc-sites-design.md` (builds on `openspec/expeditions-worksites-design.md`)

## Global Constraints
- `Source/Logic` has no Verse, RimWorld or UnityEngine references.
- **DLC gating:** every new site def carries `MayRequire` for its DLC on the def element. Quest roots and loot lines from other DLCs carry `MayRequire` on their `li`.
- **Work sites keep their rules:** 60% stash, 30% on Partial, the site removed on every outcome, no retaliation.
- **Save compatibility:**
  - `Opportunity.linkedSite` keeps the save label `"worksite"`.
  - `Exp_WorkSite` keeps its defName.
  - Squads already out on work sites resolve with no migration.
- **No quest signals:** never fire a quest's own signals. End quests with `quest.End(..., sendLetter: false)`.
- **Factions:** no hidden faction in `Opportunity.faction`. `Resolver.Capture` never picks a hidden or temporary faction.
- **`Rand` state:** consequences and raids run outside `Rand.PushState`. Loot is rolled inside it, as now.
- **Retaliation:** `pointsFactor` 0.5, `delayTicks` 30000, retries daily for 3 days with no home map, then the raid is dropped.
- **Difficulty:** Fight = `max(fightMin, threatPoints × fightPerThreatPoint)`, with defaults 100 and 1.
- **Build and test:**
  - Build: `dotnet build Source/Expeditions.csproj -c Release`. Commit `1.7/Assemblies` with each code task. If RimWorld is running, the copy fails: commit the source and note it in the ledger.
  - Tests: `dotnet test Source/Tests/Expeditions.Tests.csproj`.
  - Site: `python site/build.py --strict`.
- **Git:** work on `main`, with no AI attribution in commits.

## Review Focus
1. **A harmful site after a Failure.** It must come back on the board once the squad is home, and only once. A second squad must not be sent while the first is out. Task 2 makes `Linked` cover active expeditions for every link def. Task 6 checks this in game.
2. **A pending raid across save and load, and when the colony is gone.** A raid stored on a returning expedition, then moved to the game's list, must survive a reload. If the squad comes home as a caravan, the raid fires at the next home map. With no home map, it retries and is then dropped without errors. Task 7 checks this in game.
3. **A problem causer site with no causer thing** (`conditionCauser` null, or the part missing). `Conclude` must still end the quest and destroy the site. Task 6 null-guards it.
4. **A quest that ended while the squad was out** (done by caravan, or failed elsewhere). `Alive` requires the quest to be Ongoing, so the squad turns back and nothing is resolved. Task 2 owns this, and Task 3's in-game check covers it.
5. **A mod def whose quest root isn't loaded** (DLC off). It must never match, never throw, and log no config errors. Task 2's `ConfigErrors` skips null roots. The XML uses `MayRequire` per root.

---

### Task 1: Threat and raid math

**Files:**
- Create: `Source/Logic/SiteLinkMath.cs`
- Create: `Source/Tests/SiteLinkMathTests.cs`
- Also commit `openspec/expeditions-dlc-sites-design.md` and this plan.

**Interfaces:**
- Produces:
  - `static double SiteLinkMath.FightRequirement(double threatPoints, double perPoint, double min)`
  - `static double SiteLinkMath.RetaliationPoints(double threatNow, double factor, double floor)`

- [ ] **Step 1: Write the failing tests**

```csharp
public class SiteLinkMathTests
{
    [Fact]
    public void Fight_follows_threat_points()
    {
        Assert.Equal(450, SiteLinkMath.FightRequirement(450, 1, 100), 9);
        Assert.Equal(225, SiteLinkMath.FightRequirement(450, 0.5, 100), 9);
        Assert.Equal(100, SiteLinkMath.FightRequirement(40, 1, 100), 9);
        Assert.Equal(100, SiteLinkMath.FightRequirement(0, 1, 100), 9);
    }

    [Fact]
    public void Retaliation_is_half_the_raid_points_with_a_floor()
    {
        Assert.Equal(1000, SiteLinkMath.RetaliationPoints(2000, 0.5, 300), 9);
        Assert.Equal(300, SiteLinkMath.RetaliationPoints(400, 0.5, 300), 9);
    }
}
```

- [ ] **Step 2: Run them and see them fail.** Expected: build error, `SiteLinkMath` not defined.
- [ ] **Step 3: Implement both functions as `max(min, threat × perPoint)` and `max(floor, threatNow × factor)`.**
- [ ] **Step 4: Run the tests.** Expected: all pass (28 in total).
- [ ] **Step 5: Commit:** "Add threat and retaliation math for linked world sites".

### Task 2: Generalise the work site link (no behaviour change)

**Files:**
- Create: `Source/Defs/SiteLinkProps.cs`, `Source/World/SiteLinkWorker.cs`, `Source/World/SiteLinkWorker_Worksite.cs`, `Source/World/SiteLinks.cs`
- Delete: `Source/World/WorksiteLink.cs`. Its code moves into `SiteLinks` and `SiteLinkWorker_Worksite`.
- Modify:
  - `Source/Defs/ExpeditionSiteDef.cs`: remove `worksiteOnly`, add `link`, and `FromWorldOnly => rescueOnly || link != null`
  - `Source/Core/Opportunity.cs`: rename `worksite` to `linkedSite` (save label `"worksite"`). `Requirement` and `Label` go through the worker.
  - `Source/World/OfferGenerator.cs`, `Source/Resolve/Resolver.cs`, `Source/Resolve/LootRoller.cs`, `Source/Core/ExpeditionsGame.cs`, `Source/Squad/Departure.cs`, `Source/UI/Window_Expeditions.Opportunities.cs`: every `WorksiteLink` and `worksite` use
  - `Defs/Expeditions_Sites.xml`: `Exp_WorkSite` replaces `worksiteOnly` with `<link><workerClass>Expeditions.SiteLinkWorker_Worksite</workerClass><questRoots><li>OpportunitySite_WorkSite</li></questRoots></link>`
  - `site/build.py`: a def with a `link` element goes to page group `worksites` when its worker is the work site worker, else to `worldsites`. `site_link` maps both to `finds`.

**Interfaces:**
- Produces:
  - `class SiteLinkProps`, with the fields as in the spec's *The link block*:
    - `Type workerClass = typeof(SiteLinkWorker)`
    - `List<QuestScriptDef> questRoots`, `List<SitePartDef> siteParts`
    - `bool failureKeepsSite`, `affectRelations`, `raidCredit`
    - `float fightPerThreatPoint = 1`, `fightMin = 100`
    - `List<ThingDefCountClass> rewardCounts`
    - `ThingSetMakerDef? lootMaker`, `int lootMakerRolls = 1`
    - `RetaliationProps? retaliation`
    - `SiteLinkWorker Worker` (lazy, with `def` set)
  - `class RetaliationProps { RaidSource source; PawnKindDef? animalKind; float pointsFactor = 0.5f; int delayTicks = 30000; }`
  - `enum RaidSource { SiteFaction, Mechanoids, Insects, Pirates, Manhunters }`, in `SiteLinkProps.cs`
  - `class SiteLinkWorker`, with `ExpeditionSiteDef def` and these virtuals:
    - `bool Matches(Site s, Quest q)`: the root is in `questRoots`, and when `siteParts` is set, any part is listed
    - `bool CanOffer(Site s)`: default true
    - `float FightRequirement(Site s)`
    - `List<Thing> MakeLoot(Expedition e, Outcome outcome)`: default empty
    - `IEnumerable<string> InfoLines(Opportunity o)`: default none
    - `void Conclude(Site s, Quest? q, Outcome outcome, ExpeditionReport report, List<Pawn> raiders)`
    - `bool RemovesOnFailure => !def.link!.failureKeepsSite`
  - `class SiteLinkWorker_Worksite : SiteLinkWorker`. Today's behaviour:
    - `CanOffer`: non-empty stash
    - `FightRequirement`: guards × `worksiteFightPerGuard`
    - `MakeLoot`: the stash share
    - `InfoLines`: guards, "At best", the hostility warning
    - `Conclude`: what `WorksiteLink.Raid` does today
  - `static class SiteLinks`:
    - `Quest? QuestOf(Site s)`: the ongoing quest with a `QuestPart_SpawnWorldObject` whose `worldObject == s`
    - `ExpeditionSiteDef? DefFor(Site s, Quest q)`
    - `bool Alive(Site? s)`: spawned, not destroyed, no map, and its quest is Ongoing
    - `bool Linked(Site s)`, `bool Lost(Expedition e)`, `void TurnBack(Expedition e)`
    - `int TicksLeft(Site s)`, `void Tick()`
  - `static List<Thing> LootRoller.Make(ThingDef def, int count)`: today's private `Add`, now public and returning the things

- [ ] **Step 1: Write the types and move the code.**
  - `SiteLinks.Tick` scans `Find.WorldObjects.Sites`. For each live site with a quest, it finds the def via `DefFor`, checks range and `Linked`, then checks `worker.CanOffer`, and makes the offer:
    - `faction` is `s.Faction`, or null when that faction is hidden
    - `expiresTick` comes from `TicksLeft`
  - `Lost` and `TurnBack` are today's code, keyed on `opp.site.link != null`.
  - `Resolver.Resolve` keeps rolling inside the pushed state: outcome, fates, then `LootRoller.Roll` (whose link branch calls `MakeLoot` then `Fit`).
  - After `Rand.PopState`, when `opp.linkedSite` is alive, it runs `Conclude` with the surviving humanlike squad, then clears `linkedSite` and any temporary `faction`.
  - `Resolver.Capture` also skips hidden factions.
- [ ] **Step 2: Config errors.** `SiteLinkProps.ConfigErrors`: `questRoots` is non-empty after nulls are removed, and `workerClass` is a `SiteLinkWorker`. Call it from `ExpeditionSiteDef.ConfigErrors`.
- [ ] **Step 3: Build.** Expected: 0 errors. Tests stay green, and `python site/build.py --strict` passes.
- [ ] **Step 4: Check in game.** Run the work site checks from the work site plan's Tasks 3-5:
  - the offer and its preview
  - Success at 60%
  - the site removed and the owner hostile
  - turning back
  - a save made before the change loads, with a work site squad out, and that squad still resolves
- [ ] **Step 5: Commit:** "Generalise the work site link into site link workers".

### Task 3: The base worker and the Odyssey loot-table sites

**Files:**
- Modify: `Source/World/SiteLinkWorker.cs` (base behaviour)
- Modify: `Defs/Expeditions_Sites.xml` (new defs)

**Interfaces:**
- Consumes: `SiteLinkMath.FightRequirement` (Task 1), and Task 2's types.
- Produces: the base worker behaviour that Tasks 4-6 extend:
  - `FightRequirement`: `SiteLinkMath.FightRequirement(threat, fightPerThreatPoint, fightMin)`, with `threat` = the max of the parts' `parms.threatPoints`, or `site.ActualThreatPoints` when that is 0
  - `MakeLoot`:
    - the def's `loot` table, via the same rules as `LootRoller` today: chance, `successOnly`, category pick and tech limit, with no Partial halving
    - plus the `Reward_Items` items of the quest's first `QuestPart_Choice` choice, taken from the reward and copied with `ThingMaker`, so vanilla's own objects aren't moved
  - `InfoLines`: "At best:" with the max count of every chance-1 `loot` entry, and the `Reward_Items`
  - `Conclude`:
    1. raid credit if `raidCredit` and the outcome isn't Failure
    2. `q.End(Success)` on non-Failure, or `q.End(Fail)` on Failure when `RemovesOnFailure`
    3. `s.Destroy()` unless the outcome is Failure and `failureKeepsSite`

- [ ] **Step 1: Implement the base behaviour as above.**
- [ ] **Step 2: Add the defs.** Add `Exp_AncientWarehouse`, `Exp_AncientRefinery`, `Exp_AncientGarrison`, `Exp_AncientLaunchSite`, `Exp_InfestedSettlement`, `Exp_AncientMercenaries` and `Exp_AlphaThrumbo` exactly as in the spec's Prize table:
  - `MayRequire="Ludeon.RimWorld.Odyssey"`, quest roots, challenges, loot, risk family, `retaliation` source
  - mercenaries get `raidCredit` true
  - thrumbo loot: `AlphaThrumboHorn` 1, `Leather_AlphaThrumbo` 60~120, `Meat_AlphaThrumbo` 150~300

  Read the Odyssey data to confirm the meat def is generated (`Meat_<race>`). If not, use the race's `meatDef`.
- [ ] **Step 3: Confirm who owns the spawn.** In `Data/Odyssey/Defs/QuestScriptDefs`, check that each `questRoots` entry is the quest whose script spawns the site (`QuestNode_SpawnWorldObjects`, or a root class calling `quest.SpawnWorldObject`). If a Giver spawns it through a subquest, list the quest that holds the spawn part. Ledger the result.
- [ ] **Step 4: Build.** Expected: 0 errors, and the site build passes.
- [ ] **Step 5: Check in game with Odyssey.** Use the dev quest tool for `Opportunity_AncientStructureWarehouse`, `OpportunitySite_AncientMercenaries` and `OpportunitySite_AlphaThrumbo_Giver`:
  - each shows one offer with its "At best"
  - Success brings the table loot (full, not halved), and the site and quest are gone
  - Failure removes the site with the quest failed
  - finishing the quest by caravan while the squad is out turns the squad back
- [ ] **Step 6: Commit:** "Link Odyssey ancient structures, mercenaries and the alpha thrumbo".

### Task 4: Gravcore sites (rewards listed on the quest)

**Files:**
- Create: `Source/World/SiteLinkWorker_QuestReward.cs`
- Modify: `Defs/Expeditions_Sites.xml` (`Exp_GravcoreSite`, `Exp_GravcoreCaves`)

**Interfaces:**
- Consumes: the base worker (Task 3).
- Produces: `SiteLinkWorker_QuestReward : SiteLinkWorker`.
  - `MakeLoot` and `InfoLines` add, for each `Reward_DefinedThingDef` in the quest's first choice, its `thingDef` with the count from `rewardCounts`, or 1 if none is listed.

- [ ] **Step 1: Implement the worker.**
- [ ] **Step 2: Add the two defs** from the spec, with `rewardCounts` GravlitePanel 10. Before writing it, check the panel count against the gravcore map content in `Data/Odyssey/Defs` (the layouts or genstep that place the panels). Ledger what you found.
- [ ] **Step 3: Build.** Expected: 0 errors.
- [ ] **Step 4: Check in game.** Generate `Gravcore_MechanoidRelay` and `Gravcore_InsectLair`:
  - the offer shows "Gravcore ×1, gravlite panel ×10" plus the table
  - Success brings them home
  - the insect lair's quest ends once, as Success, with no letter
- [ ] **Step 5: Commit:** "Link Odyssey gravcore sites".

### Task 5: Ancient complexes (vanilla loot maker)

**Files:**
- Create: `Source/World/SiteLinkWorker_Complex.cs`
- Modify: `Defs/Expeditions_Sites.xml` (`Exp_MechanitorComplexSite`, `Exp_AncientComplexSite`)
- Modify: `Languages/English/Keyed/Expeditions.xml`

**Interfaces:**
- Consumes: the base worker.
- Produces: `SiteLinkWorker_Complex : SiteLinkWorker`.
  - `MakeLoot` adds `lootMakerRolls` × `lootMaker.root.Generate(new ThingSetMakerParams())`.
  - `InfoLines` adds `Exp_LinkExtraLoot`: "plus {0}", with the loot maker's label, or "ancient complex loot" when it has none.

- [ ] **Step 1: Implement the worker and add the key.**
- [ ] **Step 2: Add the defs.**
  - Mechanitor complex: `MayRequire` Biotech, root `OpportunitySite_AncientComplex_Mechanitor`, loot `Mechlink` 1, `lootMaker` `MapGen_AncientComplexRoomLoot_Better`, `lootMakerRolls` 3.
  - Ancient complex: `MayRequire` Ideology, roots `OpportunitySite_AncientComplex` and `OpportunitySite_AncientComplex_Giver`, the same loot maker.
  - Both: harsh no-captors risk and mechanoid retaliation.
  - The `lootMaker` reference needs Ideology, so give the mechanitor complex `MayRequire="Ludeon.RimWorld.Biotech,Ludeon.RimWorld.Ideology"` only if the loot maker is Ideology-only. If Biotech also defines it, use plain Biotech. Check `Data/Biotech/Defs/ThingSetMakerDefs` and ledger the result.
- [ ] **Step 3: Build.** Expected: 0 errors.
- [ ] **Step 4: Check in game.**
  - Generate `OpportunitySite_AncientComplex_Mechanitor`: Success brings the mechlink and room loot.
  - Failure removes the site with the quest failed.
  - Nothing in the report or log mentions map generation.
- [ ] **Step 5: Commit:** "Link ancient complexes with vanilla room loot".

### Task 6: Royalty problem causers (harmful sites)

**Files:**
- Create: `Source/World/SiteLinkWorker_ProblemCauser.cs`
- Modify: `Defs/Expeditions_Sites.xml` (`Exp_ProblemCauser`)

**Interfaces:**
- Consumes: the base worker.
- Produces: `SiteLinkWorker_ProblemCauser : SiteLinkWorker`.
  - `Conclude` on non-Failure:
    1. `q.End(Success)`
    2. for every part with a `conditionCauser`: `End()` each of its `CompCauseGameCondition.CausedConditions` (null-guarded), then `conditionCauser.Destroy(DestroyMode.QuestLogic)` unless it is already destroyed
    3. raid credit
    4. `s.Destroy()`
  - On Failure it does nothing: the site and quest stay.

- [ ] **Step 1: Implement the worker.**
- [ ] **Step 2: Add `Exp_ProblemCauser`** from the spec:
  - `MayRequire` Royalty, root `ProblemCauser`, `failureKeepsSite` true, `raidCredit` true, no loot, harsh risk
  - retaliation from `SiteFaction`, which resolves to mechanoids when the site faction is the mechanoid faction
  - also set `captured` to 0 when the faction is mechanoid: `Resolver.Capture` already won't pick a hidden faction, and mechanoids aren't hidden, so check `Faction.OfMechanoids` in `Capture` and skip it. Ledger this.
- [ ] **Step 3: Build.** Expected: 0 errors.
- [ ] **Step 4: Check in game.**
  - Generate `ProblemCauser` with a psychic droner. On Success, the droner's condition is gone within a fifth of a day, the site and quest are gone, and no error is logged.
  - Generate another and force Failure: the site stays, and its offer is back on the board once the squad is home, exactly once.
- [ ] **Step 5: Commit:** "Link Royalty problem causer sites".

### Task 7: Retaliation raids

**Files:**
- Create: `Source/World/Retaliation.cs`
- Modify: `Source/Core/Expedition.cs` (`PendingRaid? retaliation`, saved), `Source/Core/ExpeditionsGame.cs` (`List<PendingRaid> pendingRaids`, saved, plus the fire loop in `Tick250`), `Source/Resolve/Resolver.cs` (store on Partial), `Source/Resolve/Arrival.cs` (hand over on every return path), `Source/Core/ExpeditionReport.cs` and `Arrival.ReportText` / `Window_ExpeditionReport` (the warning line), `Languages/English/Keyed/Expeditions.xml`

**Interfaces:**
- Consumes: `SiteLinkMath.RetaliationPoints` (Task 1), `RetaliationProps` (Task 2).
- Produces:
  - `class PendingRaid : IExposable { RaidSource source; Faction? faction; PawnKindDef? animal; float pointsFactor; int fireTick; Map? map; int retries; }`
  - `static class Retaliation`:
    - `PendingRaid? For(Expedition e)`: built at resolve on Partial
    - `void Hand(Expedition e, Map? home)`: moves it to the game's list with `fireTick = now + delay`
    - `void Tick()`: fires due raids
  - `string? ExpeditionReport.retaliationText`

- [ ] **Step 1: Store and hand over.**
  - `Resolver`: on Partial with `link?.retaliation`, set `e.retaliation`. The faction is kept only for `SiteFaction` when it's non-temporary and non-hidden. Otherwise the source falls back to Pirates for SiteFaction, or to Mechanoids when the site faction was the mechanoid faction.
  - Set `report.retaliationText`, from `Exp_RetaliationLine`: "{0} will answer for this within a day."
  - `Arrival.Return` calls `Retaliation.Hand(e, map)` on every path, including `BecomeCaravan`, but not when everyone was lost.
- [ ] **Step 2: Fire.** `Retaliation.Tick()` runs from `Tick250` outside any `Rand` state. For each due raid:
  - **Map:** the stored map if it's still a player home, else `Find.AnyPlayerHomeMap`. With none, set `fireTick += 60000`, `retries++`, and drop the raid after 3 retries.
  - **Points:** `SiteLinkMath.RetaliationPoints(StorytellerUtility.DefaultThreatPointsNow(map), pointsFactor, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))`.
  - **Incident by source:**
    - Mechanoids, Pirates, SiteFaction: `IncidentDefOf.RaidEnemy`. The faction falls back to `Find.FactionManager.RandomEnemyFaction()`, and the raid is skipped if there is none.
    - Insects: `IncidentDefOf.Infestation`, unless the insect faction has a Combat group maker.
    - Manhunters: `IncidentDefOf.ManhunterPack`, with `parms.pawnKind` set to the stored animal, else the map biome's default.
  - If `TryExecute` fails, retry on the next day under the same 3-retry limit.
- [ ] **Step 3: Build.** Expected: 0 errors.
- [ ] **Step 4: Check in game.**
  - Force Partial on an ancient warehouse. The report has the warning line, and about half a day after the squad gets home, a mechanoid raid at about half the usual points arrives.
  - Save in between and reload: the raid still comes.
  - Repeat with the squad coming home as a caravan (abandon the colony map): no error, and the raid comes once a home map exists, or is dropped after 3 days.
- [ ] **Step 5: Commit:** "Send a retaliation raid after a partial win at a world site".

### Task 8: Player docs

**Files:**
- Modify: `site/content/finds.md`, `site/build.py` (labels for new item defs if the build asks for them)

- [ ] **Step 1: Write the section.** Add a "DLC quest sites" section below the work sites, with `{{ sites("worldsites") }}`. It explains:
  - full loot on Success
  - loot plus a raid on Partial
  - a Failure abandons a prize site or leaves a harmful one
  - Fight follows threat points
  - orbital sites aren't included

  Run it through the humanizer skill.
- [ ] **Step 2: Build the site.** Run `python site/build.py --strict`. Expected: no problems. Add any missing item labels it reports to the tables at the top of `build.py`.
- [ ] **Step 3: Commit:** "Document DLC quest site expeditions".
