# Ideology update: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Seven Ideology sites, each offered only when the colony's main ideoligion wants it, built on new generic pieces: an ideoligion gate, mood and certainty effects, vanilla deed credit (raid, charity, conversion), supplies a squad must carry, and captives that come home as slaves or prisoners.

**Architecture:** All in Expeditions. New gate and effect classes go next to the existing ones (`Source/Defs/SiteGate.cs`, `Source/Defs/Effects.cs`, the latter split into a new `Source/Defs/IdeoEffects.cs`). Two new site fields, `supplies` and `captives`, plug into departure, resolve and arrival the way `joiners` does. The seven sites, their thoughts and the charity patch are XML that loads only with Ideology. Entries from other DLCs and mods carry `MayRequire`.

**Tech Stack:** C# (net10.0 against RimWorld 1.7's Assembly-CSharp), RimWorld XML defs and patches, xunit tests over `Source/Logic`.

**Spec:** `openspec/expeditions-ideology-update-design.md`.

## Global Constraints
- Pure math goes in `Source/Logic`, with no Verse, RimWorld or UnityEngine, and has tests in `Source/Tests`.
- Without Ideology: no errors, no new offers. Every new site def has `MayRequire="Ludeon.RimWorld.Ideology"`, and so do the thought, event and patch defs.
- `MayRequire` package ids: `Ludeon.RimWorld.Biotech`, `Ludeon.RimWorld.Anomaly`, `Ludeon.RimWorld.Odyssey`, `Sarg.AlphaMemes`, `VanillaExpanded.VMemesE`. `Ritualist` is Anomaly.
- Old saves load. New saved fields have defaults, and lists are null-checked in `PostLoadInit`.
- Every call into effect code goes through `Effects.Safe`, as today.
- User-visible strings go in `Languages/English/Keyed/Expeditions.xml` with `Exp_` keys.
- Commit messages carry no AI attribution.
- Build: `dotnet build Source/Expeditions.csproj`. Tests: `dotnet test Source/Tests`.

## Review Focus
- The squad dies on site or on the road, or every person leaves peacefully (`unescorted`), with captives in the holder: the captives must not count as the squad, walk home alone, or keep the expedition alive. Expected: they go free to the world.
- Captives come home to a slaver colony and no squad member who made it back is a free adult (only slaves or hirees survived): they stay prisoners and the line says so. Nothing throws.
- Prisoners set down at the map edge try to escape at once unless their escape timer is set, as vanilla does for caravan prisoners.
- Alms run with a squad whose own trip food is short: the launch check counts trip food and alms food together, and the food line shows both, so the squad never leaves with alms it then eats.
- The colony reforms its ideoligion while a rustling squad is out: the offer list drops what no longer fits, and the squad already out finishes and still counts the raid.
- Rustling in a biome where none of the listed farm animals live wild: the herd still comes, picked evenly from the list, not weighted by the biome's wild commonality (which is zero and would throw).

---

### Task 1: Logic rules and tests
**Files:** Create `Source/Logic/IdeoMath.cs`. Modify `Source/Logic/Enums.cs` and `Languages/English/Keyed/Expeditions.xml` (`Exp_Fate_Taken`, "taken captive"). Test: `Source/Tests/IdeoMathTests.cs`.
**Produces:**
- `IdeoMath.GateOpen(bool anyListed, bool anyMatched, bool blocked) -> bool`: closed when blocked. Otherwise open when nothing is listed to match, or something matched.
- `PawnFate.Taken`: "never rolled: someone the squad brought home against their will".
- [ ] Tests:
  - `GateOpen(true, true, false)` is true. `GateOpen(true, false, false)` is false.
  - `GateOpen(true, true, true)` is false. `GateOpen(false, false, false)` is true. `GateOpen(false, false, true)` is false.
- [ ] `dotnet test Source/Tests` passes, then commit.

### Task 2: Ideoligion gate, faith-aware factions, listed herd animals
**Files:** Modify `Source/Defs/SiteGate.cs`, `Source/Defs/ExpeditionSiteDef.cs` (FactionNeed, fields, `TamedAnimalsSpec`, ConfigErrors), `Source/World/OfferGenerator.cs`, `Source/Resolve/Resolver.cs` (`Recruits`, the animal pick).
**Consumes:** `IdeoMath.GateOpen`.
**Produces:**
- `SiteGate_Ideo : SiteGate` with fields `List<PreceptDef> anyPrecept`, `List<MemeDef> anyMeme`, `bool approvesSlavery`, `List<RitualPatternDef> anyRitualPattern`, `List<PreceptDef> noPrecept`, `List<MemeDef> noMeme`.
  - `public bool Matches(Ideo? ideo)`: false for null. Otherwise `IdeoMath.GateOpen(listed, matched, blocked)`:
    - A precept match is `ideo.HasPrecept`, and a meme match is `ideo.HasMeme`.
    - `approvesSlavery` matches when any precept has `def.approvesOfSlavery`.
    - A ritual match is any `Precept_Ritual` whose `sourcePattern` is in the list.
  - `Open()`: `ModsConfig.IdeologyActive && Matches(Faction.OfPlayer.ideos?.PrimaryIdeo)`.
  - `ClosedReason`: "no matching ideoligion" when nothing matched, or the name of the blocking precept or meme.
  - `ConfigErrors`: an error when nothing is listed in any list and `approvesSlavery` is false.
- `FactionNeed.Any` ("any faction that isn't the player's"). `Wants` already returns true for it through the default arm.
- `ExpeditionSiteDef.otherFaith` (bool): a faction is only picked when its `ideos?.PrimaryIdeo` is not null and isn't the player's main ideoligion. Applied inside `NearbyFaction`, so a same-faith settlement is never chosen. It goes through a new optional `Func<Faction, bool>? also` parameter, and every caller passes `s.otherFaith ? OtherFaith : null`. Also checked in `StillFits` against `o.faction`.
- `TamedAnimalsSpec.kinds` (`List<PawnKindDef>`): when not empty, `Candidates(biome)` returns it as is, and `Recruits` picks from it evenly (`RandomElement`) instead of by `biome.CommonalityOfAnimal`.
- [ ] Build, then commit.

### Task 3: Effects for faith and deeds
**Files:** Create `Source/Defs/IdeoEffects.cs`. Modify `Source/Defs/ExpeditionSiteDef.cs` (`JoinerSpec.joinEvent`) and `Source/Resolve/Resolver.cs` (`Recruits`). Add keys.
**Consumes:** `SiteGate_Ideo.Matches`.
**Produces.** Default outcomes: the new thought, certainty and colony-thought effects default to Success and Partial. The rest default as their base classes do (`PawnEffect`: all three; `OutcomeEffect`: Success) unless stated.
- `static class Faith`:
  - `Ideo? Primary => Faction.OfPlayer?.ideos?.PrimaryIdeo`.
  - `bool PawnMatches(ExpeditionSiteDef site, Pawn p)`: every `SiteGate_Ideo` in `site.gates` matches `p.Ideo`. True when the site has none.
  - `Pawn? Doer(Expedition e)`: the first living humanlike away pawn with `Faction == Faction.OfPlayer` that isn't a captive. History events need a doer of the colonists' own faction, or `PreceptComp_KnowsMemoryThought` gives nobody the thought.
- `PawnEffect_Thought { ThoughtDef thought; bool onlyIfFaithMatches; }`: gives the memory when the pawn has `needs.mood` and, if `onlyIfFaithMatches` is set, `Faith.PawnMatches`. No report line.
- `PawnEffect_Certainty { float offset = 0.1f; bool onlyIfFaithMatches = true; }`: `p.ideo?.OffsetCertainty(offset)`. One report line for the squad, "Exp_CertaintyLine", added once per report.
- `OutcomeEffect_ColonyThought { ThoughtDef thought; bool onlyIfFaithMatches; }` (on arrival): every colonist at home, `PawnsFinder.AllMaps_FreeColonistsSpawned`, with `needs.mood`, filtered the same way. The returning squad isn't spawned yet, so it's left out. Line "Exp_ColonyThoughtLine" ({0} = the thought's label).
- `OutcomeEffect_RaidCredit` (outcomes `Success`, `Partial`): `IdeoUtility.Notify_PlayerRaidedSomeone` with the living humanlike squad members, captives left out (Task 5 gives `Expedition.IsCaptive`; until then it uses every living humanlike). Line "Exp_RaidCreditLine".
- `OutcomeEffect_HistoryEvent { HistoryEventDef eventDef; string? lineKey; }`: records `new HistoryEvent(eventDef, doer.Named(HistoryEventArgsNames.Doer))` with `Faith.Doer(e)`, or with no args when there is none. Adds `lineKey.Translate()` when set.
- `OutcomeEffect_FactionGoodwill { int amount; }`: `e.opp.faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, true, true, null, null)` when the faction is still there and not defeated. Line "Exp_GoodwillFactionLine" ({0} = name, {1} = signed amount).
- `OutcomeEffect_DevelopmentPoints { int points = 1; }`: for a fluid `Faith.Primary`, `development.TryAddDevelopmentPoints(points)`. Line "Exp_DevelopmentLine" ({0} = points) when it returns true.
- `JoinerSpec.joinEvent` (`HistoryEventDef?`): recorded once per humanlike joiner, with `Faith.Doer(e)` as doer. Joiners are generated in the player's faction, so they already hold its ideoligion; nothing to convert.
- [ ] Build, then commit.

### Task 4: Supplies the squad carries and gives away
**Files:** Create `Source/Squad/SupplyPlanner.cs`. Modify `Source/Defs/ExpeditionSiteDef.cs` (`SiteSupplies`, the `supplies` field), `Source/Squad/Departure.cs` (`CanLaunch`, `Launch`), `Source/UI/Window_Expeditions.Opportunities.cs` (`RefreshEvaluation`, the drawer), and `Source/Resolve/Resolver.cs`. Add keys.
**Produces:**
- `SiteSupplies { float nutrition; int medicine; List<Outcome> spentOn = { Success, Partial }; }` and `ExpeditionSiteDef.supplies` (`SiteSupplies?`). Config error when both amounts are 0 or below.
- `SupplyPlanner.FoodNeed(List<Pawn> squad, double tripDays, ExpeditionSiteDef site) -> float`: `FoodPlanner.Required + (site.supplies?.nutrition ?? 0)`. `CanLaunch`, `Launch` and `RefreshEvaluation` all use it for the food check and the pickups. `Expedition.foodNeeded` stays the trip need alone.
- `SupplyPlanner.Medicine(List<Pawn> squad, Map map, int count, out List<(Thing thing, int count)> pickups) -> int`: the same shape as `FoodPlanner.Available`. It counts inventory medicine first (`def.IsMedicine`), then the nearest reachable unforbidden `ThingRequestGroup.Medicine`.
- `SupplyPlanner.Spend(List<Pawn> carriers, float nutrition, int medicine) -> (float nutrition, int medicine)`: nutrition through `FoodPlanner.ConsumeOnReturn`, which is changed to return the nutrition it removed. Medicine goes cheapest first. Returns what was actually given. The carriers are every living non-vehicle squad pawn, pack animals included, because `FoodPlanner.Available` counted their inventories too.
- [ ] `CanLaunch` returns "Exp_NotEnoughMedicine" ({0} needed, {1} have) when the medicine is short.
- [ ] `Launch` queues the medicine pickups after the food ones.
- [ ] The drawer shows "Exp_Supplies" ({0} = nutrition, {1} = medicine) under the food line when the site has supplies, red when the medicine is short. The food line already includes the alms.
- [ ] `Resolve`: when the outcome is in `spentOn`, after fates, `Spend` from those carriers. Line "Exp_SuppliesGivenLine" ({0}, {1} = given).
- [ ] Build, then commit.

### Task 5: Captives
**Files:** Modify `Source/Defs/ExpeditionSiteDef.cs` (`CaptivesSpec`, the `captives` field, `HasFinds`), `Source/Core/Expedition.cs` (the `captives` list, `IsCaptive`, save), `Source/Resolve/Resolver.cs`, `Source/Resolve/Arrival.cs`, `Source/Squad/SquadEvents.cs` (`People`), `Source/Defs/IdeoEffects.cs` (raid credit leaves out captives). Add keys.
**Consumes:** `PawnFate.Taken`, `Faith.Primary`.
**Produces:**
- `CaptivesSpec { float chance = 1f; IntRange count = new(1, 1); float partialFactor; }`. Config error when `count.min < 1`.
- `Expedition.captives` (`List<Pawn>`, saved by reference, nulls removed on load) and `Expedition.IsCaptive(Pawn p) -> bool`.
- [ ] In `Resolve`, after `Recruits`: roll with `HookMath.JoinerChance` when found and the outcome isn't Failure, and only if a living humanlike non-captive is left in the squad. For each captive:
  - Generate a pawn with `e.opp.faction?.RandomPawnKind() ?? PawnKindDefOf.Villager`, in `e.opp.faction` unless it's null or defeated (then no faction), on `e.opp.tile`. NPC faction defs don't set `basicMemberKind`, so it can't be used.
  - Strip it: `equipment?.DestroyAllEquipment()` and `inventory?.DestroyAll()`. `SetGuestStatus` can't drop gear for a pawn that isn't spawned.
  - Set it to `guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner)`, add it to contents and to `e.captives`, and give it report fate `Taken`.
- [ ] Captives never count as the squad:
  - `SquadEvents.People` leaves them out.
  - In the `Resolve` check "nobody left, come home now", they don't count as people.
- [ ] At the top of `Arrival.Return`: if no living humanlike non-captive is left, free the captives before anything else. Each is removed from contents and from `e.captives`, given `SetGuestStatus(null)` and passed to the world, and the report gets "Exp_CaptivesFreedLine". That covers the squad lost on the road, wiped out on site, and the `unescorted` path, and keeps them out of `pawns`, food and the caravan.
- [ ] Arrival when the squad is home, after the outcome effects and before `Release` or `BecomeCaravan`:
  - If `Faith.Primary` has a precept with `approvesOfSlavery` and a warden exists (a living `Eligibility.IsOverlord` pawn from the squad), call `GenGuest.TryEnslavePrisoner(warden, p)` for each living captive. Line "Exp_CaptivesEnslavedLine" ({0} = count).
  - Otherwise they stay prisoners. Line "Exp_CaptivesPrisonersLine" ({0} = count), or "Exp_CaptivesNoWardenLine" when slavery fits but there's no warden.
- [ ] Right after `Release` on a map: each captive still a prisoner gets `guest.WaitInsteadOfEscapingForDefaultTicks()`, as vanilla does for caravan prisoners. Then clear `e.captives`. In the caravan case, just clear it.
- [ ] Build, then commit.

### Task 6: Defs, thoughts, charity patch, seven sites, keys
**Files:**
- Create `Defs/Expeditions_Sites_Ideology.xml`: the sites, thoughts and the history event, every def with `MayRequire="Ludeon.RimWorld.Ideology"`.
- Create `Patches/Expeditions_Charity.xml`.
- Modify `Languages/English/Keyed/Expeditions.xml`.
**Consumes:** everything above.
- [ ] `HistoryEventDef Exp_CharityFulfilled_Alms` (label "alms given").
- [ ] Charity thoughts. Three `ThoughtDef`s, `Exp_CharityFulfilled_{Worthwhile,Important,Essential}_Alms`, with `durationDays` 8 and mood 1 / 2 / 4. Each has the label "alms given" and a description in the vanilla tone, rising with the level.
- [ ] Charity patch: a `PatchOperationFindMod` (`<mods><li>Ideology</li></mods>`) wraps a sequence of three `PatchOperationAdd`s. Patch operations don't read `MayRequire`. Each add is on `Defs/PreceptDef[defName="Charity_X"]/comps` and adds a `PreceptComp_KnowsMemoryThought` with `eventDef` `Exp_CharityFulfilled_Alms` and that level's thought.
- [ ] `AM_NonViolence` and `AM_Violence_Abhorrent_Strict` load only when Alpha Memes and VME are both active, so they get `MayRequire="Sarg.AlphaMemes,VanillaExpanded.VMemesE"`.
- [ ] Site thoughts, memory thoughts with one stage each:

  | Def | Mood | Days |
  |---|---|---|
  | `Exp_RustledHerd` | +4 | 6 |
  | `Exp_GaveAlms` | +3 | 6 |
  | `Exp_MadePilgrimage` | +8 | 15 |
  | `Exp_PilgrimsReturned` | +3 | 6 |
  | `Exp_PreachedToStrangers` | +4 | 6 |
  | `Exp_HumbledHeretics` | +5 | 8 |
  | `Exp_SavedGrove` | +6 | 10 |

  Labels and descriptions are plain and neutral. The pilgrimage thought says "made the journey".
- [ ] The seven sites, numbers as below. Gates, "never for" lists and `MayRequire`s are exactly as in the design's per-site sections. Each site's thoughts use `onlyIfFaithMatches` where the design says "filtered".

  | Site | Parent | Needs | Challenges (weight / requirement) | Finds and effects |
  |---|---|---|---|---|
  | `Exp_Rustling` | Harsh | `needsFaction` Friendly; biomes TemperateForest, AridShrubland, BorealForest, Tundra | Stealth 2 / 120; Tame 1 / 100; Fight 0.5 / 100 | `tamedAnimals` with kinds Cow, Goat, Sheep, Horse, Yak, Alpaca, count 3~8, chance 1. Raid credit. Goodwill −15 on Partial and Failure. Thought `Exp_RustledHerd`. The herd comes only on Success, as tamed animals always do: on a Partial the squad was seen and ran. |
  | `Exp_AlmsRun` | Gentle | none | Endure 1.5 / 100; Haul 1 / 100; Teach 0.5 / 100 | `supplies` 25 nutrition, 5 medicine. `goodwillAllFriendly` 2. Joiners chance 0.25, Villager, partialFactor 0. History event `Exp_CharityFulfilled_Alms` with line "Exp_CharityLine" on Success and Partial. Thought `Exp_GaveAlms`. |
  | `Exp_Pilgrimage` | Gentle | none | Endure 2 / 150 | `daysOnSite` 3. Pawn thought `Exp_MadePilgrimage` and certainty +0.1 (both filtered). Colony thought `Exp_PilgrimsReturned` (filtered). One development point. Success and Partial. |
  | `Exp_PreachingTour` | Gentle | `needsFaction` Friendly, `otherFaith` | Debate contest 2 / 100; Endure 0.5 / 100 | `goodwillReward` 5. Joiners chance 0.3, Villager, `joinEvent` ConvertedNewMember. Certainty +0.05 (filtered). Thought `Exp_PreachedToStrangers`. Goodwill −10 on Failure. |
  | `Exp_HereticShrine` | Harsh | `needsFaction` Hostile, `otherFaith` | Fight 2 / 250; Stealth 1 / 100 | Loot: Silver 150~400, Gold 10~30 (chance 0.5). Raid credit. Goodwill −20 on Success and Partial. Thought `Exp_HumbledHeretics` for squad and colony (filtered). |
  | `Exp_FelledGrove` | Harsh | biomes TemperateForest, BorealForest, TropicalRainforest, TemperateSwamp, TropicalSwamp | Fight 2 / 200; Endure 0.5 / 100 | Loot: GauranlenSeed 1, `successOnly`. Colony thought `Exp_SavedGrove` (filtered) on Success and Partial. |
  | `Exp_Captives` | Harsh | `needsFaction` Any | Stealth 2 / 120; Fight 1 / 150 | `captives` chance 0.9, count 1~3, partialFactor 0.5. Raid credit. Goodwill −25 on Success and Partial. |

  Every site sets `findChanceBase` 1, `squadSize` 2~5 (alms and pilgrimage 1~4), and `daysOnSite` 1 unless stated. Each gets a description written as something seen or heard: the design's italic lines.
- [ ] Keys from Tasks 3–5, in the style of the existing lines.
- [ ] Build. Then start RimWorld once (Ideology, no mods) and read `Player.log` for errors from Expeditions: XML errors, config errors, missing `MayRequire`d defs. Fix and repeat. Commit.

### Task 7: Whole-branch review and in-game check
- [ ] A fresh reviewer (Fable) checks the branch against the design and this plan. Fix the findings, then build and run the tests again.
- [ ] In game, with dev mode: spawn each site through the debug actions and force Success. Check the report lines, the thoughts and, for captives, the slave or prisoner status. Check `Player.log`.
