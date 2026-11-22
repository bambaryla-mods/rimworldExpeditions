# Who Can Go: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decide who may join an expedition squad, and handle hirees, slaves and creepjoiners correctly while the squad is away.

**Architecture:** One `Eligibility` class answers "may this pawn go, and why not" for the picker and the launch check. Two small reflection readers (`CommRecruiterCompat`, `CreepJoinerCompat`) see into the other mod and vanilla's private creepjoiner state. A `SquadEvents` class runs from the 250-tick loop and handles everything that can happen mid-trip: a pawn pulled out of the holder, hostile hirees, creepjoiner downsides, slaves losing their overlord. Pure numbers (contract fit, fight odds) live in `Source/Logic` with unit tests.

**Tech Stack:** C# (net10.0 target, RimWorld 1.7), HarmonyLib `AccessTools` for reflection, xunit for Logic tests.

**Spec:** `openspec/expeditions-squad-eligibility-design.md`

## Global Constraints
- Expeditions owns all of this. CommRecruiter is not changed and not referenced at compile time.
- Nothing in the picker, the odds, the tooltips or a refusal reason may hint that a pawn is a creepjoiner.
- A non-retained CommRecruiter hiree never counts as an overlord.
- `Source/Logic` files use no Verse, RimWorld or UnityEngine types (the test project compiles them without game references).
- All numbers live in `Expeditions_Tuning.xml` or on challenge defs, not in code.
- Reflection failure is fail-safe: a hiree is then refused like any quest lodger, and a creepjoiner is treated as an ordinary colonist. Log one warning, never throw.
- New save fields load with safe defaults on old saves.
- Commit messages: plain sentences in the repo's style ("Refuse quest guests on expeditions"), no attribution lines.

## Decisions made while planning (not in the spec)
- **Suppression is read at resolution, not stored at launch.** Away pawns are suspended, so their needs do not move; reading `Need_Suppression.CurLevel` at resolution gives the launch value without a new save field.
- **Hostile hirees act in any phase after Departing**, including Returning (where they can make off with loot). The early end only applies while Outbound or OnSite. This follows the user's "why wouldn't they take all my shit".
- **A pawn pulled out by an unknown mod** gets a report line and the trip continues. Only a known hiree triggers the betrayal path.
- **Slave release off-map** is done by hand, not with `GenGuest.SlaveRelease` (that one needs a map to find an exit): clear guest status, set the faction to the pawn's `SlaveFaction` when it is a living non-player faction, else `null`, then pass to the world.
- **Creepjoiner downside types are matched by worker class**, not def name, so modded downsides that reuse the vanilla workers are covered: `CreepJoinerWorker_DepartDownside` = Leaves, `CreepJoinerWorker_AggressiveDownside` = Aggressive. Aggressive kinds by the `aggressive` def's worker: `CreepJoinerWorker_FleshbeastEmergence`, `CreepJoinerWorker_SightstealerAttack`, anything else = Assault.

## Review Focus
1. **A squad whose pawn left the holder through our own code** (killed, captured, ran away) must not be mistaken for a pulled-out pawn. Every removal we make goes through `Expedition.Forget`, which drops the pawn from `squad` too.
2. **A save made mid-trip with a hiree** must reload with `hirees` intact and no null entries.
3. **CommRecruiter not installed** (or renamed types) must leave the mod working: no hirees exist, lodger rule applies, one warning at most.
4. **A creepjoiner whose tracker is "disabled" in vanilla terms** (duplicated, or ever lost ego) must never fire a downside on the trip.
5. **Fights with no one left to fight** (squad all pack animals, enemy strength 0) must not divide by zero or kill animals for no reason.

Each has a test or check in the owning task.

---

### Task 1: Logic, enums, tuning and keys

**Files:**
- Create: `Source/Logic/SquadMath.cs`
- Create: `Source/Tests/SquadMathTests.cs`
- Modify: `Source/Logic/Enums.cs` (add `Outcome.CutShort`, `PawnFate.RanAway`)
- Modify: `Source/Defs/ExpeditionMiscDefs.cs` (`ExpeditionTuningDef` fields)
- Modify: `Defs/Expeditions_Tuning.xml`
- Modify: `Languages/English/Keyed/Expeditions.xml`
- Modify: `Source/UI/Window_ExpeditionReport.cs` (banner colour for `CutShort`, grey like `Gone`)
- Modify: `Source/Debug/ExpeditionsDebug.cs:139` (exclude `CutShort` from "Force next outcome", like `Gone`)

**Interfaces:**
- Produces:
  - `SquadMath.ContractFits(int ticksLeft, double tripDays, double marginDays) -> bool` (`ticksLeft >= (tripDays + marginDays) * 60000`)
  - `SquadMath.FightRatio(double squad, double enemy) -> double` (`enemy <= 0` returns `double.PositiveInfinity`; `squad <= 0` with positive enemy returns 0)
  - `SquadMath.SquadWins(double ratio, double roll01, Curve winCurve) -> bool` (`roll01 < winCurve.Evaluate(ratio)`; infinite ratio always wins)
  - `Outcome.CutShort`, `PawnFate.RanAway` (appended at the end of each enum so saved ints keep their meaning)
  - Tuning fields with these exact names and defaults:
    - `contractMarginDays = 1f`
    - `slaveRunawayCurve` (suppression 0..1 → chance): `(0, 0.4) (0.5, 0.15) (1, 0.03)`
    - `turnedCreepjoinerFactor = 1.5f`
    - `fleshbeastFight = 150f`
    - `sightstealerPointsFactor = 0.45f` (vanilla rolls 0.25–0.65 of threat points)
    - `slipAwayRequirement = 100f`
    - `fightWinCurve` (ratio → squad win chance): `(0.5, 0.15) (1, 0.5) (2, 0.85) (3, 0.97)`
    - `fightWoundCurve` (ratio → wound severity 0..1): `(0.5, 0.9) (1, 0.6) (3, 0.1)`
    - `fightDeathCurve` (ratio → death chance per member): `(0.5, 0.4) (1, 0.15) (3, 0)`
  - Curve accessors in the style of `RatioCurve`: `SlaveRunawayCurve`, `FightWinCurve`, `FightWoundCurve`, `FightDeathCurve`; `ConfigErrors` checks each has points.

- [ ] **Step 1: Write the failing tests** in `Source/Tests/SquadMathTests.cs`:

```csharp
[Fact] public void Contract_fits_with_margin()
{
    Assert.True(SquadMath.ContractFits(6 * 60000, 5, 1));
    Assert.False(SquadMath.ContractFits(6 * 60000 - 1, 5, 1));
}
[Fact] public void Ratio_handles_zero_sides()
{
    Assert.Equal(double.PositiveInfinity, SquadMath.FightRatio(100, 0));
    Assert.Equal(0, SquadMath.FightRatio(0, 100));
    Assert.Equal(2, SquadMath.FightRatio(200, 100));
}
[Fact] public void Infinite_ratio_always_wins()
{
    var c = new Curve(new[] { (0.5, 0.15), (1.0, 0.5), (3.0, 0.97) });
    Assert.True(SquadMath.SquadWins(double.PositiveInfinity, 0.999, c));
    Assert.True(SquadMath.SquadWins(1, 0.49, c));
    Assert.False(SquadMath.SquadWins(1, 0.51, c));
}
```

- [ ] **Step 2: Run** `dotnet test Source/Tests/Expeditions.Tests.csproj` — expect a build failure (no `SquadMath`).
- [ ] **Step 3: Implement** `SquadMath`, the enum values, the tuning fields, accessors and XML values above. Language keys to add (wording is the builder's, these names are fixed): `Exp_Outcome_CutShort` ("They turned back before the job was done."), `Exp_Fate_RanAway` ("ran away").
- [ ] **Step 4: Run tests and build** — `dotnet test Source/Tests/Expeditions.Tests.csproj` all pass; `dotnet build Source/Expeditions.csproj` 0 errors.
- [ ] **Step 5: Commit** "Add squad fight and contract math, new outcome and fate"

---

### Task 2: Reflection readers

**Files:**
- Create: `Source/Squad/CommRecruiterCompat.cs`
- Create: `Source/Squad/CreepJoinerCompat.cs`

**Interfaces:**
- Produces:
  - `CommRecruiterCompat.TryGetHiree(Pawn p, out int ticksLeft, out bool retained) -> bool`. True only when CommRecruiter is loaded and has a record for `p`. Finds the game component by type full name `CommRecruiter.GameComponent_CommRecruiter` in `Current.Game.components`, calls `RecordFor(Pawn)`, reads `TicksLeft` (property) and `retained` (field) of the returned `HireeRecord`. Members are resolved once with `AccessTools` and cached; any failure logs one warning (`[Expeditions] CommRecruiter support off: ...`) and returns false from then on.
  - `enum CreepDue { None, Leaves, Assault, Fleshbeasts, Sightstealers }`
  - `CreepJoinerCompat.Due(Pawn p, int now) -> CreepDue`. `None` unless: `p.IsCreepJoiner`, `p.creepjoiner.downside != null`, private `triggeredDownside` false, private `duplicated` false, `p.everLostEgo` false, private `downsideTriggersAt != 0 && now >= downsideTriggersAt`, and the downside worker is Depart (→ `Leaves`) or Aggressive (→ by aggressive worker, and private `triggeredAggressive` false).
  - `CreepJoinerCompat.MarkFired(Pawn p, bool aggressive)` sets `triggeredDownside = true`, plus `triggeredAggressive = true` when aggressive, plus `hasLeft = true` for Leaves, so vanilla does not fire it again at home.
  - Private fields read with `AccessTools.FieldRefAccess<Pawn_CreepJoinerTracker, T>`; missing field → one warning, `Due` returns `None` forever.

- [ ] **Step 1: Implement both files.** No unit tests (game types); Step 2 is the check.
- [ ] **Step 2: Build** `dotnet build Source/Expeditions.csproj` — 0 errors. Grep the decompiled `Pawn_CreepJoinerTracker` field names once more against the strings used (`downsideTriggersAt`, `triggeredDownside`, `triggeredAggressive`, `duplicated`, `hasLeft`).
- [ ] **Step 3: Commit** "Read CommRecruiter contracts and creepjoiner timers"

---

### Task 3: Eligibility, children, picker and launch

**Files:**
- Create: `Source/Squad/Eligibility.cs`
- Modify: `Source/Defs/ExpeditionChallengeDef.cs` (add `public float childFactor = 1f;`)
- Modify: `Defs/Expeditions_Challenges.xml` (`<childFactor>0.5</childFactor>` on `Exp_Fight`, `Exp_Contest_Shooting`, `Exp_Contest_Melee`, `Exp_Haul`)
- Modify: `Source/Squad/SquadEvaluator.cs`
- Modify: `Source/Squad/Departure.cs` (`CanLaunch`, `Launch`)
- Modify: `Source/Core/Expedition.cs` (`hirees`, `Forget`)
- Modify: `Source/UI/Window_Expeditions.Opportunities.cs` (`AddOptions`)
- Modify: `Languages/English/Keyed/Expeditions.xml`

**Interfaces:**
- Consumes: `CommRecruiterCompat.TryGetHiree`, `SquadMath.ContractFits`, tuning `contractMarginDays`.
- Produces:
  - `Eligibility.IsChild(Pawn p) -> bool` (`p.DevelopmentalStage == DevelopmentalStage.Child`)
  - `Eligibility.IsHiree(Pawn p) -> bool` (a non-retained hiree)
  - `Eligibility.IsOverlord(Pawn p) -> bool` (humanlike, player faction, not a slave, adult, not mutant, not `IsHiree`, not dead)
  - `Eligibility.Listed(Pawn p) -> bool` (false for babies; they are not shown at all)
  - `Eligibility.CanGo(Pawn p, double tripDays) -> AcceptanceReport`, checks in this order: mutant → `Exp_NoGoMutant`; hiree not retained → contract fit or `Exp_NoGoContract` (args: contract time left, trip days); other `IsQuestLodger()` → `Exp_NoGoGuest`. Creepjoiners, slaves and children pass.
  - `Eligibility.SquadOk(IEnumerable<Pawn> squad) -> AcceptanceReport`: any slave and no overlord → `Exp_NeedOverlord`.
  - `SquadEvaluator.Score(IEnumerable<Pawn> pawns, ExpeditionChallengeDef def) -> double` — humanlikes only (unless `isHaul`), each pawn's score times `childFactor` for a child, combined by `def.combine`. `Evaluate` uses the same per-pawn path so the picker and fights agree.
  - `Expedition.hirees: List<Pawn>` (saved as references, nulls removed on load) and `Expedition.Forget(Pawn p)` (removes from `contents` if held, and from `squad` and `hirees`; calls `HolderChanged`).

- [ ] **Step 1: Implement `childFactor`** in `SquadEvaluator`: multiply the child's `PawnScore` before adding it to `scores`, and append a line `Exp_ChildFactor` ("Child: ×{0}") to that pawn's last `Contribution` explanation for the challenge, so the tooltip says why. Vehicle cargo and the armed bonus stay unscaled.
- [ ] **Step 2: Implement `Eligibility`** with the signatures above.
- [ ] **Step 3: Picker.** In `AddOptions`, filter candidates with `Eligibility.Listed`; for each humanlike call `CanGo(p, tripDays >= 0 ? tripDays : Departure.TripDays(Map, o, new List<Pawn> { p }, ride))`. Refused pawns are added as a disabled `FloatMenuOption` (`action: null`) whose label is `"{name} ({reason})"`. Allowed pawns keep today's label and action.
- [ ] **Step 4: Launch check.** In `Departure.CanLaunch`, after the busy/unavailable checks: every humanlike passes `CanGo(p, trip)` (use the trip days computed for the squad) else return its reason; then `SquadOk(squad)`. In `Departure.Launch`, fill `e.hirees` with the squad members where `Eligibility.IsHiree`.
- [ ] **Step 5: Keys.** `Exp_NoGoMutant` ("can't go"), `Exp_NoGoGuest` ("quest guest, can't leave"), `Exp_NoGoContract` ("contract ends in {0}, trip takes {1}"), `Exp_NeedOverlord` ("Slaves need a free colonist of your own to keep them in line."), `Exp_ChildFactor`.
- [ ] **Step 6: Build and test** — `dotnet build Source/Expeditions.csproj` 0 errors; `dotnet test Source/Tests/Expeditions.Tests.csproj` all pass.
- [ ] **Step 7: Commit** "Refuse quest guests and ghouls, check hiree contracts, require slave overlords, weaken children"

---

### Task 4: Report lines and the early end

**Files:**
- Modify: `Source/Core/ExpeditionReport.cs` (`events`, `lostText`)
- Modify: `Source/Resolve/Arrival.cs` (`ReportText`, lost letter)
- Modify: `Source/UI/Window_ExpeditionReport.cs` (list `events` under the outcome lines)
- Modify: `Source/World/SiteLinks.cs` (`TurnBack`)
- Modify: `Source/Resolve/Resolver.cs` (`Kill`, `Capture` use `Forget`)

**Interfaces:**
- Produces:
  - `ExpeditionReport.events: List<string>` (saved, defaults to empty) and `ExpeditionReport.lostText: string?` (saved).
  - `ExpeditionReport.SetFate(Pawn p, PawnFate fate, List<string>? injuries = null)`: updates the pawn's existing `PawnResult` or adds one, appending injuries.
  - `SiteLinks.TurnBack(Expedition e, Outcome outcome = Outcome.Gone) -> ExpeditionReport` — same timing and food as today; the report's outcome is the argument; it returns the report so callers add lines. Existing callers unchanged.
  - `Arrival.Return` uses `report.lostText` for the lost letter text when set.
  - `Resolver.Kill` and `Resolver.Capture` call `e.Forget(p)` instead of `e.contents.Remove(p)`.

- [ ] **Step 1: Implement** the fields, `SetFate`, the `TurnBack` parameter and return, the `events` lines in `ReportText` (after `successText`) and in the report window (above the pawn list; include them in the scroll height), the `lostText` override, and the `Forget` calls.
- [ ] **Step 2: Check Review Focus 1** — grep `contents.Remove(` across `Source/`: every remaining call that takes a *squad pawn* out must be `Forget`. `Arrival` and `Departure.Cancel` release things on purpose and stay as they are.
- [ ] **Step 3: Build and test**, as before.
- [ ] **Step 4: Commit** "Report lines and a cut-short outcome for trips that end early"

---

### Task 5: Slaves

**Files:**
- Create: `Source/Squad/SquadEvents.cs` (slave part; Task 6 adds the rest)
- Modify: `Source/Resolve/Resolver.cs`

**Interfaces:**
- Consumes: `Eligibility.IsOverlord`, `Expedition.Forget`, `ExpeditionReport.SetFate`, tuning `SlaveRunawayCurve`.
- Produces:
  - `SquadEvents.RunAway(Expedition e, Pawn p, ExpeditionReport report)` — `Forget`, free the slave off-map (see *Decisions*), pass to world (`KeepForever`), `report.SetFate(p, PawnFate.RanAway)`.
  - `SquadEvents.CheckOverlord(Expedition e, ExpeditionReport report)` — if any living slave is held and no held pawn `IsOverlord`, every living slave runs; adds event line `Exp_SlavesFled`.
  - `SquadEvents.SlaveRoll(Expedition e, ExpeditionReport report)` — each living held slave runs when `Rand.Value < SlaveRunawayCurve.Evaluate(suppression)`; suppression from `p.needs.TryGetNeed<Need_Suppression>()?.CurLevel ?? 1`.

- [ ] **Step 1: Implement** the three methods.
- [ ] **Step 2: Hook into `Resolver.Resolve`**, inside the seeded `Rand` block after the fates and before loot: `SquadEvents.CheckOverlord(e, report)` then `SquadEvents.SlaveRoll(e, report)`. (Check first: slaves with no overlord all run, no roll.) The slaves' fate entries from `ApplyFate` are kept and overwritten to `RanAway` via `SetFate`; their XP stays.
- [ ] **Step 3: Key** `Exp_SlavesFled` ("With nobody left to keep them in line, the slaves fled.").
- [ ] **Step 4: Build and test.**
- [ ] **Step 5: Commit** "Slaves can run away on expeditions"

---

### Task 6: Mid-trip events: pulled pawns, hirees, creepjoiners

**Files:**
- Modify: `Source/Squad/SquadEvents.cs`
- Modify: `Source/Core/ExpeditionsGame.cs` (`Tick250`)
- Modify: `Languages/English/Keyed/Expeditions.xml`

**Interfaces:**
- Consumes: everything above; `InjuryMaker.Apply`; `SquadEvaluator.Score`; `SiteLinks.TurnBack`; `CreepJoinerCompat.Due/MarkFired`; `SquadMath`; `Arrival.Return`; `SiteLinkMath.FightRequirement(points, 1, 100)` to turn sightstealer points into a Fight score.
- Produces:
  - `SquadEvents.Tick(Expedition e)` — called from `Tick250` for each expedition in `Outbound`, `OnSite` or `Returning`, before the phase switch. Returns `bool` "expedition ended" so `Tick250` skips the switch for it.
  - `SquadEvents.Fight(Expedition e, ExpeditionReport report, double enemy) -> bool` (squad won). Fighters: held humanlikes, not dead. Squad score `SquadEvaluator.Score(fighters, Exp_Fight)`. Ratio and win via `SquadMath`. For each fighter: death when `Rand.Value < FightDeathCurve(ratio)` → kill as `Resolver` does (make `Resolver.Kill` `internal`); else wounds `InjuryMaker.Apply(p, severity, allowPartLoss: severity > 0.5f)` with `severity = Clamp01(FightWoundCurve(ratio) * Rand.Range(0.7f, 1.3f))`, fate `Injured` when any wound was made. No fighters → returns false without touching anyone (Review Focus 5). Runs inside `Rand.PushState(e.opp.Seed ^ now)`.
  - `SquadEvents.EndEarly(Expedition e) -> ExpeditionReport` — `Outbound`/`OnSite`: `SiteLinks.TurnBack(e, Outcome.CutShort)`. `Returning`: returns the existing `e.report` (created if null) and changes nothing else.

**Pulled pawns.** In `Tick`: `missing = e.squad.Where(p => !e.contents.Contains(p))`. For each, `Forget` it. Then:
- Missing hirees (in `e.hirees`) whose faction is now hostile: the betrayal, once for the whole group:
  - Report from `EndEarly`. If no held humanlike is left: set `report.lostText` (`Exp_HireesTookAll`, with the faction name), add the event line, call `Arrival.Return(e)`, return true.
  - Else roll slip-away: chance `RatioCurve.Evaluate(SquadEvaluator.Score(hirees, Exp_Stealth) / slipAwayRequirement)`. Success: take food then other non-pawn, non-building `contents` until the hirees' combined `MassUtility.Capacity` is used; destroy what is taken; event line `Exp_HireesSlipped` with the list. Failure: `Fight(e, report, SquadEvaluator.Score(hirees, Exp_Fight))`. Won: each hiree is killed (world pawn `Kill`) or flees on a 50/50 seeded roll, event line `Exp_HireesBeaten`. Lost: pack animals pass to the hirees' faction as world pawns, ride things (`Building` contents and vehicles) and cargo are destroyed, `e.ride = Ride.Foot`, and if still `Outbound`/`OnSite` the turn-back timing is recomputed on foot (call `EndEarly` after setting the ride); event line `Exp_HireesWon`.
  - Then `CheckOverlord`.
- Any other missing pawn: event line `Exp_PawnPulled` (into `e.report` when Returning, else `pendingEvents`, see the note below) and carry on.

**Creepjoiners.** For each held pawn with `CreepJoinerCompat.Due(p, now) != None` (one per tick is enough):
- `Leaves`: `MarkFired(p, false)`, `Forget`, `SetFaction(null)`, pass to world, fate `RanAway`, event line `Exp_CreepLeft`. Into `e.report` when Returning, else `pendingEvents`/`pendingFates` (see note). Then `CheckOverlord`.
- `Assault` / `Sightstealers` / `Fleshbeasts`: `MarkFired(p, true)`, `Forget` the creepjoiner first (they are the enemy), report from `EndEarly`. Enemy: Assault `Score([p], Exp_Fight) * turnedCreepjoinerFactor`; Fleshbeasts `fleshbeastFight`; Sightstealers `FightRequirement(StorytellerUtility.DefaultThreatPointsNow(home) * sightstealerPointsFactor, 1, 100) + Score([p], Exp_Fight)` where `home` is `e.originMap` if still a map, else `Find.AnyPlayerHomeMap`, else 100 points. `Fight(...)`. Creepjoiner fate: Fleshbeasts → killed; others → killed if the squad won, else `SetFaction(Faction.OfEntities)` and pass to world (`RanAway`). Event line per kind: `Exp_CreepAssault`, `Exp_CreepFleshbeasts`, `Exp_CreepSightstealers`, plus `Exp_FightWon` or `Exp_FightLost`. Then `CheckOverlord`. If no held humanlike is left, `Arrival.Return(e)` and return true.

**Note on reports before resolution.** A Leaves or a generic pull while `Outbound`/`OnSite` happens before resolution makes `e.report`. Keep those lines in a new `Expedition.pendingEvents: List<string>` (saved) and pending fates in `pendingFates: List<PawnResult>` (saved, deep); `Resolver.Resolve` copies both into its report first. `EndEarly` copies them into the `TurnBack` report the same way.

- [ ] **Step 1: Implement `Fight` and `EndEarly`**, and `pendingEvents`/`pendingFates` with their copy into `Resolver` and `EndEarly`.
- [ ] **Step 2: Implement pulled-pawn handling and the betrayal.**
- [ ] **Step 3: Implement the creepjoiner handling.**
- [ ] **Step 4: Hook `SquadEvents.Tick` into `Tick250`**, wrapped in try/catch that logs `[Expeditions] Squad event failed: ...` once per expedition id and carries on, so one bad expedition never stops the loop.
- [ ] **Step 5: Keys** for every line named above, with `{0}` arguments for pawn names, faction names and item lists. Wording must describe only what just happened (no hint of hidden creepjoiner traits before the event).
- [ ] **Step 6: Check Review Focus 2 and 4** — `Expedition.ExposeData` saves `hirees`, `pendingEvents`, `pendingFates` with `??=` and null cleanup on `PostLoadInit`; `Due` returns `None` for `duplicated` and `everLostEgo`.
- [ ] **Step 7: Build and test.**
- [ ] **Step 8: Commit** "Hirees, creepjoiners and runaway slaves while the squad is away"

---

### Task 7: Docs and build output

**Files:**
- Modify: `site/content/guide.md` (a short "Who can go" section: guests and ghouls refused, hirees need a long enough contract, slaves need one of your own free adults and may run, children are weaker in fights and carry less; nothing about creepjoiners)
- Modify: `1.7/Assemblies/Expeditions.dll`, `.pdb` (release build)

- [ ] **Step 1: Write the section**, in the voice of the existing guide.
- [ ] **Step 2: Build** `dotnet build Source/Expeditions.csproj` and run the tests one last time.
- [ ] **Step 3: Commit** "Document who can go on expeditions" with the rebuilt assemblies.

---

### After the tasks
- Whole-branch review by a fresh reviewer (Fable).
- Start RimWorld, reach the main menu (and load a save if one is available), read `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log` for errors and exceptions that mention Expeditions or come from its defs, fix, repeat until clean, close the game.
