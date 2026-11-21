# Ideology Work Sites Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ideology work sites within range appear on the expedition board. A squad resolves one under the normal rules and brings home at most 60% of its stash.

**Architecture:** `OfferGenerator.Tick` scans the world for work site `Site`s and makes an `Opportunity` linked to each (`opp.worksite`). One shared def, `Exp_WorkSite`, carries the challenges. Its Fight requirement is replaced per offer from the guard count. Loot comes from the linked site's stash through a pure share function. Resolution applies vanilla's raid consequences and removes the site. An expedition whose site disappears turns back with a new `Outcome.Gone`.

**Tech Stack:** C# (net10.0, `LangVersion latest`), RimWorld 1.7 Assembly-CSharp, xunit for `Source/Logic`.

**Spec:** `openspec/expeditions-worksites-design.md`

## Global Constraints
- `Source/Logic` has no Verse, RimWorld or UnityEngine references (the test project compiles it without them).
- Work site support is inert without Ideology: the def is `MayRequire="Ludeon.RimWorld.Ideology"` and its DefOf field is `[MayRequireIdeology]`, nullable.
- `worksiteShare` = 0.6, `worksiteFightPerGuard` = 90, minimum Fight requirement 100. Both values go in `Expeditions_Tuning.xml`.
- Partial halves the share (rounded, minimum 1), Failure gives nothing.
- Work site offers never count toward `offerSlots` and have `expiresTick = -1`.
- No vanilla patches (no Harmony).
- Build: `dotnet build Source/Expeditions.csproj -c Release`. Tests: `dotnet test Source/Tests/Expeditions.Tests.csproj`. The built `1.7/Assemblies/Expeditions.dll` is tracked, so commit it with each code task.
- Work directly on `main`. Commit messages have no AI attribution.

## Review Focus
1. **The same work site offered twice.** It must not happen, whether the squad is out on it (`Departure.Launch` removes the offer from the board) or a save is reloaded. Task 3 tests the "already linked" check via the dev action log.
2. **A save made while a squad is out, and the site destroyed before load** (time-out across a reload). `Scribe_References` gives `worksite == null`, which must count as gone, not crash. Task 5 treats null as gone.
3. **A work site whose stash is empty or whose owner is defeated.** No offer for an empty stash. A defeated owner still resolves, and `AffectRelationsOnAttacked` is skipped for a defeated or null faction. Tasks 3 and 4.
4. **Partial on a tiny stash.** A stash of 1 gives a best case of 0. That must be 0 items on every outcome, not a minimum of 1. Task 1 tests it.
5. **The player opens the site map (caravan raid) while the squad is on site.** It counts as gone (`HasMap`), and the squad must not also resolve. Task 5 checks during OnSite as well as Outbound.

---

### Task 1: Share and requirement math

**Files:**
- Create: `Source/Logic/WorksiteMath.cs`
- Create: `Source/Tests/WorksiteMathTests.cs`
- Also: commit the design and this plan with the task.

**Interfaces:**
- Produces: `static int WorksiteMath.Share(int stash, double share, Outcome outcome)` and `static double WorksiteMath.FightRequirement(int guards, double perGuard, double min = 100)`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class WorksiteMathTests
{
    [Fact]
    public void Share_by_outcome()
    {
        Assert.Equal(1200, WorksiteMath.Share(2000, 0.6, Outcome.Success));
        Assert.Equal(600, WorksiteMath.Share(2000, 0.6, Outcome.Partial));
        Assert.Equal(0, WorksiteMath.Share(2000, 0.6, Outcome.Failure));
        Assert.Equal(1, WorksiteMath.Share(2, 0.6, Outcome.Success));   // floor(1.2)
        Assert.Equal(1, WorksiteMath.Share(2, 0.6, Outcome.Partial));   // half of 1, at least 1
        Assert.Equal(0, WorksiteMath.Share(1, 0.6, Outcome.Success));   // floor(0.6): nothing to take
        Assert.Equal(0, WorksiteMath.Share(1, 0.6, Outcome.Partial));
        Assert.Equal(0, WorksiteMath.Share(2000, 0.6, Outcome.Gone));
    }

    [Fact]
    public void Fight_scales_with_guards()
    {
        Assert.Equal(450, WorksiteMath.FightRequirement(5, 90), 9);
        Assert.Equal(100, WorksiteMath.FightRequirement(1, 90), 9);
        Assert.Equal(100, WorksiteMath.FightRequirement(0, 90), 9);
    }
}
```

- [ ] **Step 2: Run them and see them fail.** Run `dotnet test Source/Tests/Expeditions.Tests.csproj`. Expected: build error, `WorksiteMath` and `Outcome.Gone` not defined.
- [ ] **Step 3: Add `Gone` at the end of `Outcome` in `Source/Logic/Enums.cs`** (comment: never rolled, the site was gone when the squad got there). Implement `WorksiteMath`. `Share` is `best = floor(stash × share)`. Success gives `best`. Partial gives `best == 0 ? 0 : max(1, round(best × 0.5))`. Anything else gives 0. `FightRequirement` is `max(min, guards × perGuard)`.
- [ ] **Step 4: Run the tests and see them pass.** Expected: all pass, including the existing ones.
- [ ] **Step 5: Commit** the design, this plan, `Enums.cs`, `WorksiteMath.cs` and the test: "Add work site share and fight requirement math".

### Task 2: The site def, tuning, and a per-offer Fight requirement

**Files:**
- Modify: `Source/Defs/ExpeditionSiteDef.cs` (new `worksiteOnly` flag and `FromWorldOnly` property)
- Modify: `Source/Defs/ExpeditionMiscDefs.cs` (tuning fields, DefOf)
- Modify: `Source/Core/Opportunity.cs` (`worksite` link, label, requirement)
- Modify: `Source/Squad/SquadEvaluator.cs`, `Source/Resolve/Resolver.cs:28`, `Source/UI/Window_Expeditions.Opportunities.cs:66,111,140,180`, `Source/Debug/ExpeditionsDebug.cs:20,77,101,176`, `Source/World/OfferGenerator.cs:73`, `Source/Prospect/ProspectService.cs:70`
- Modify: `Defs/Expeditions_Sites.xml`, `Defs/Expeditions_Tuning.xml`

**Interfaces:**
- Consumes: `WorksiteMath.FightRequirement` (Task 1).
- Produces:
  - `bool ExpeditionSiteDef.worksiteOnly` and `bool ExpeditionSiteDef.FromWorldOnly => rescueOnly || worksiteOnly`
  - `float ExpeditionTuningDef.worksiteShare = 0.6f`, `float ExpeditionTuningDef.worksiteFightPerGuard = 90f`
  - `[MayRequireIdeology] static ExpeditionSiteDef? ExpeditionsDefOf.Exp_WorkSite`
  - `Site? Opportunity.worksite` (saved with `Scribe_References`)
  - `float Opportunity.Requirement(SiteChallenge sc)`
  - `SquadEvaluator.Evaluate(IReadOnlyList<Pawn> pawns, Opportunity opp, Ride ride, RideOption? rideOption)`, which replaces the `(pawns, site, ride, rideOption, seed)` overload (it uses `opp.site` and `opp.Seed`)
  - `ChallengeResult(SiteChallenge site, double requirement, double score, double chance, List<Contribution> parts)`

- [ ] **Step 1: Add the def and tuning.** In `Expeditions_Sites.xml`, add `Exp_WorkSite` with `ParentName="Exp_SiteHarsh"` and `MayRequire="Ludeon.RimWorld.Ideology"` on the element:
  - label "work site", and a description: the guards and the stash are vanilla's, and a squad brings back at most 60%
  - `worksiteOnly` true, `findChanceBase` 1, no loot
  - challenges Fight (2, 300), Stealth (1.5, 100), Haul (1, 150). The 300 is only the value for the debug evaluator. Offers replace it.
  - `daysOnSite` 0.5, `openness` 1, `squadSize` 1~6

  Add `worksiteShare` 0.6 and `worksiteFightPerGuard` 90 to `Expeditions_Tuning.xml`, each with a one-line comment.
- [ ] **Step 2: Switch the filters.** Change every `!s.rescueOnly` (OfferGenerator line 73, ProspectService line 70, debug lines 77 and 101) to `!s.FromWorldOnly`. The debug evaluator (line 20) keeps listing all sites.
- [ ] **Step 3: Link and requirement on `Opportunity`.** `Requirement(sc)` returns `WorksiteMath.FightRequirement(guards, tuning.worksiteFightPerGuard)` when `worksite != null` and `sc.challenge == ExpeditionsDefOf.Exp_Fight`. Here `guards` is `worksite.parts[0].expectedEnemyCount`. Otherwise it returns `sc.requirement`. While the site is linked, `Label` is `worksite.LabelCap` plus ` (faction name)` when `faction != null`.
- [ ] **Step 4: The evaluator takes the offer.** `Evaluate(pawns, opp, ride, rideOption)` uses `opp.Requirement(sc)` for the chance and stores it in `ChallengeResult.requirement`. Update all four callers. The debug evaluator builds a throwaway `new Opportunity { id = 0, site = site }`. The UI's bar and tooltip and the debug log read `r.requirement` instead of `r.site.requirement`.
- [ ] **Step 5: Build.** Run `dotnet build Source/Expeditions.csproj -c Release`. Expected: 0 errors. Run the tests: still green.
- [ ] **Step 6: Check in game, without Ideology loaded if you can, or with it.** Use the dev action "Evaluate selected pawns vs site..." and pick "work site". The log shows the three challenges with Fight / 300. With Ideology off there's no "work site" entry and no errors at load.
- [ ] **Step 7: Commit:** "Add the work site def and per-offer challenge requirements".

### Task 3: Work sites appear on the board

**Files:**
- Create: `Source/World/WorksiteLink.cs`
- Modify: `Source/World/OfferGenerator.cs` (`Tick`, `ReadyMadeCount`, `StillFits`)
- Modify: `Source/UI/Window_Expeditions.Opportunities.cs` (the info column)
- Modify: `Languages/English/Keyed/Expeditions.xml`

**Interfaces:**
- Consumes: `Opportunity.worksite`, `ExpeditionsDefOf.Exp_WorkSite`, `tuning.worksiteShare`, `WorksiteMath.Share` (Tasks 1-2).
- Produces, in `static class WorksiteLink`:
  - `bool IsWorksite(Site s)`: the main part's def has tag `WorkSite`, and its worker is `SitePartWorker_WorkSite`
  - `bool Alive(Site? s)`: not null, spawned, not destroyed, `!s.HasMap`
  - `List<ThingDefCountClass> Stash(Site s)`: `parts[0].things` summed per def
  - `int Guards(Site s)`: `parts[0].expectedEnemyCount`
  - `bool Linked(Site s)`: an offer or an active expedition has `opp.worksite == s`
  - `void Tick()`

- [ ] **Step 1: Implement `WorksiteLink`.** `Tick()` returns at once when `Exp_WorkSite` is null. Otherwise, for each `Find.WorldObjects.Sites` that is a work site, alive, in range of `Find.AnyPlayerHomeMap` (`WorldArea.InRange` with `Settings.range`), has a non-empty `Stash`, and isn't `Linked`, it adds:

  ```csharp
  new Opportunity { id = NextId(), site = Exp_WorkSite, tile = s.Tile, worksite = s, faction = s.Faction, expiresTick = -1 }
  ```

  Log each one added with `Log.Message("[Expeditions] Work site offer ...")` while `Prefs.DevMode` is on.
- [ ] **Step 2: Wire it into `OfferGenerator`.**
  - `Tick` calls `WorksiteLink.Tick()` after the expiry sweep and before the `nextOfferTick` check.
  - `ReadyMadeCount` also skips `o.worksite != null`.
  - `StillFits` returns `WorksiteLink.Alive(o.worksite)` for an offer with `site.worksiteOnly`. A null link counts as gone.
- [ ] **Step 3: The offer panel.** For a linked offer, below the description, show two lines:
  - `Exp_WorksiteGuards`: "{0} guards"
  - `Exp_WorksiteHaul`: "At best: {0}". Each stash item is shown as `label ×WorksiteMath.Share(count, worksiteShare, Success)`, and items whose share is 0 are left out.

  Move the numbers and food rows down to make room. Add both keys to the English keyed file.
- [ ] **Step 4: Build.** Expected: 0 errors.
- [ ] **Step 5: Check in game with Ideology.** Use the vanilla dev action "Generate quest" with `OpportunitySite_WorkSite`. Within a few seconds the board shows "farming work site (Faction)" or similar, with the guard count and the 60% haul. Then check:
  - "Spawn many offers... 25 random" doesn't produce a second work site offer
  - the board's own offers still reach `offerSlots`
  - after save and reload, still one offer
  - destroying the site with the dev tool (world: destroy object) removes the offer within a few seconds
- [ ] **Step 6: Commit:** "Show Ideology work sites on the expedition board".

### Task 4: Resolving a work site

**Files:**
- Modify: `Source/Resolve/LootRoller.cs`
- Modify: `Source/Resolve/Resolver.cs`
- Modify: `Source/World/WorksiteLink.cs` (`Raid`)
- Modify: `Source/Core/ExpeditionReport.cs`, `Source/Resolve/Arrival.cs:92-107`, `Source/UI/Window_ExpeditionReport.cs:45-49`

**Interfaces:**
- Consumes: `WorksiteLink.Stash`, `WorksiteMath.Share`, `tuning.worksiteShare`.
- Produces:
  - `void WorksiteLink.Raid(Site s, Outcome outcome, ExpeditionReport report)`
  - `string? ExpeditionReport.relationsText` (saved)

- [ ] **Step 1: Loot.** In `LootRoller.Roll`, after the Failure return: when `e.opp.worksite` is set and alive, then for each `Stash` entry `Add(made, def, WorksiteMath.Share(count, worksiteShare, outcome))` when the result is above 0. Then go straight to `Fit`, with no loot table, target or silver. `found` is always true here (`findChanceBase` 1, no Search challenge), so don't branch on it.
- [ ] **Step 2: `Raid`.**
  1. If the faction isn't null, isn't the player and isn't defeated, call `SettlementUtility.AffectRelationsOnAttacked(s, ref text)` and store a non-empty `text` in `report.relationsText`.
  2. Find the quest: the first ongoing quest with a `QuestPart_WorldObjectTimeout` whose `worldObject == s`. End it with `QuestEndOutcome.Success` on Success or Partial (this fires `Raided_WorkSite`), and `QuestEndOutcome.Fail` on Failure.
  3. Then `s.Destroy()` if it isn't destroyed yet.
- [ ] **Step 3: Call it.** In `Resolver.Resolve`, after `Reward(...)` and inside the try: `if (e.opp.worksite is { } ws && WorksiteLink.Alive(ws)) WorksiteLink.Raid(ws, outcome, report);`.
- [ ] **Step 4: Show `relationsText`.** It goes into `Arrival.ReportText` after the goodwill lines, and into `Window_ExpeditionReport` under `successText` in the same style.
- [ ] **Step 5: Build.** Expected: 0 errors.
- [ ] **Step 6: Check in game.** Generate a work site with a neutral owner and send a squad. Use the dev actions "Force next outcome..." and "Advance expedition phase..." to step through it. Check:
  - Success brings home 60% of each stash item, minus anything left behind
  - Partial brings 30%
  - Failure brings nothing
  - the site is gone from the world map and the quest is gone from the quest tab
  - the neutral owner is now hostile, with the change in the report
  - a raider ideoligion gets the "raided" thought once, on Success
- [ ] **Step 7: Commit:** "Resolve work site expeditions with vanilla raid consequences".

### Task 5: The site is gone before the squad gets there

**Files:**
- Modify: `Source/Core/ExpeditionsGame.cs:64-86` (`Tick250`)
- Modify: `Source/UI/Window_ExpeditionReport.cs:28-33` (banner colour for `Gone`)
- Modify: `Source/Debug/ExpeditionsDebug.cs:137-140` ("Force next outcome..." must not offer `Gone`, which is never rolled)
- Modify: `Languages/English/Keyed/Expeditions.xml`

**Interfaces:**
- Consumes: `WorksiteLink.Alive`, `Outcome.Gone`.
- Produces: `static void WorksiteLink.TurnBack(Expedition e)`.

- [ ] **Step 1: `TurnBack`.**
  - Set `e.report = new ExpeditionReport { siteLabel = e.opp.Label, tick = now, outcome = Outcome.Gone, hasFinds = false }`.
  - Work out the time spent out: `now - phaseStartTick` while Outbound. While OnSite, use the outbound time instead, from `TravelMath.OutDays(e.ride, e.tripFootDays, tiles, tuning.Travel)` as `Departure` computes it.
  - Call `e.StartPhase(Phase.Returning, spent)`, then `HolderChanged()`.
- [ ] **Step 2: Check in `Tick250`.** Before the phase switch, for an expedition in Outbound or OnSite whose `opp.site.worksiteOnly` is set and `!WorksiteLink.Alive(e.opp.worksite)`, call `TurnBack(e)` and `continue`. A null `worksite` after a reload counts as gone.
- [ ] **Step 3: Copy and colour.** Add `Exp_Outcome_Gone`: "The work site was gone by the time they got there." Every `"Exp_Outcome_" + outcome` lookup then works as it is. Give `Gone` a neutral grey banner (`new Color(0.55f, 0.55f, 0.55f)`) in the report window switch. Filter `Gone` out of the "Force next outcome..." menu.
- [ ] **Step 4: Build.** Expected: 0 errors. Tests green.
- [ ] **Step 5: Check in game.** Send a squad, then destroy the site with the dev tool while they're Outbound. The Active tab shows them Returning. They arrive home with their food eaten as normal and a grey "gone" report, with no injuries and no XP. Repeat while OnSite. Then repeat by sending a caravan to attack the site (it gets a map): the squad turns back the same way, and only the caravan's raid counts.
- [ ] **Step 6: Commit:** "Turn a squad back when its work site is gone".

### Task 6: Player docs

**Files:**
- Modify: `site/content/jobs.md` or `site/content/guide.md`, whichever lists site kinds (read both first), and `README.md` if it lists features.

- [ ] **Step 1: Write the section.** Add a short "Ideology work sites" section:
  - work sites in range show up on the board
  - a squad brings home at most 60% of the stash, 30% on Partial
  - guards set the Fight requirement
  - the owner turns hostile, and the site is gone afterwards
  - if the site disappears first, the squad turns back

  Run it through the humanizer skill.
- [ ] **Step 2: Build the site.** Run `python site/build.py` (see `site/README.md`). Expected: no errors.
- [ ] **Step 3: Commit:** "Document Ideology work site expeditions".
