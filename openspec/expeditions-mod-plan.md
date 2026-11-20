# Expeditions: implementation plan

Build Expeditions, a standalone RimWorld 1.7 mod. Squads leave the map for points of interest on the colony's own flat map, results are rolled off-screen, and the survivors come home with a report.

Pure math (scoring, odds, seeds, food, loot fit, travel, projection) lives in `Source/Logic/` with no Verse, RimWorld or Unity references. It is unit tested by a separate xunit project that compiles those files directly. Everything that touches the game sits on top: defs, a game component that holds away pawns in per-expedition `ThingOwner`s, Harmony patches that keep them counted as colonists, generators, departure and return, and the window. Game-facing tasks are verified in game with dev mode, through a checklist in each task.

Tech: C# (`LangVersion latest`), .NET SDK 10 (`net10.0`, as in the author's other mods), RimWorld 1.7 `Assembly-CSharp.dll` and `UnityStubs.dll`, Lib.Harmony 2.4.3, xunit for Logic tests.

Spec: `openspec/expeditions-mod-design.md`. Mockups: `openspec/mockups/expedition-layouts.html` (layout A) and `openspec/mockups/expedition-map-styles.html` (styles 1 and 2).

## Global constraints

- RimWorld 1.7 only. Mod folder `Mods/Expeditions`, packageId `bambaryla.expeditions`, name "Expeditions".
- Hard dependency only on Harmony (`brrainz.harmony`). Vehicle Framework and [LFS] UI Rethemed (`LazyFridayStudio.UIRethemed.Core`) are optional and detected at runtime. No dependency on BOR.
- No code from Stellar Logistics (BambalLogistics) or BambaMap, and don't open them for reference. CommRecruiter is the author's own code and MAY be used as a pattern reference (its `DefInjector`, `HireLook`, `Compat/UIRethemed` layout).
- One namespace: `Expeditions`.
- `Source/Logic/**` SHALL NOT use `Verse`, `RimWorld` or `UnityEngine`. The test project enforces this because it has no game references.
- Every player-facing string is a keyed translation in `Languages/English/Keyed/Expeditions.xml`, with keys prefixed `Exp_`.
- Commits have no AI attribution or Co-Authored-By trailers.
- Opening the window must feel instant after the first bake.
- Expedition range defaults to 60 tiles. Ready-made offers: up to 5, a new one every 1-3 days, each lasting 3-8 days. The prospect seed window is 2.5 days (150,000 ticks). Success is clamped to 5%-95%.

## Review focus

1. **A squad member becomes unavailable during departure** (downed, drafted, mental break, kidnapped, killed) → they drop out of the squad. If the squad falls below the site's minimum, the expedition is cancelled, nothing is consumed, and food picked up stays in inventories. Covered in Task 9.
2. **The origin map is gone when the squad returns** → they go to another player home map. With no home map at all, they become a vanilla caravan on the origin tile, so they are never lost. Covered in Task 10.
3. **Save and load in any phase, Departing included** → the expedition resumes. Departure jobs find their expedition by id, and nothing is duplicated or lost. Covered in Tasks 6 and 9.
4. **Vanilla or modded code that walks `AllCaravansAndTravellingTransporters` meets an away pawn** (null `GetCaravan()`, invalid `Tile`) → nothing throws. Covered by the smoke checklist in Task 7.
5. **Vehicle Framework is absent, or present with a changed API** → no type-load errors. The Vehicles ride is hidden, with one warning at most. Covered in Task 9.

## File structure

```
Mods/Expeditions/
  About/About.xml
  LoadFolders.xml
  Defs/Expeditions_Challenges.xml, Expeditions_Sites.xml, Expeditions_Tuning.xml, Expeditions_Jobs.xml
  Languages/English/Keyed/Expeditions.xml
  1.7/Assemblies/Expeditions.dll                          (build output)
  Compat/UIRethemed/1.7/Assemblies/Expeditions.UIRethemed.dll (build output)
  openspec/expeditions-mod-design.md, expeditions-mod-plan.md, mockups/
  Source/
    Expeditions.csproj
    Logic/        Curve.cs, ChallengeMath.cs, OutcomeRoll.cs, LeadChance.cs, ProspectSeed.cs,
                  FoodMath.cs, LootFit.cs, TravelMath.cs, Projection.cs, Enums.cs
    Defs/         ExpeditionChallengeDef.cs, ExpeditionSiteDef.cs, ExpeditionExclusionDef.cs,
                  ExpeditionTuningDef.cs, ExpeditionsDefOf.cs
    Core/         ExpeditionsMod.cs (Mod + settings), ExpeditionsGame.cs (GameComponent),
                  Opportunity.cs, Expedition.cs, ExpeditionReport.cs, HolderPatches.cs, DefInjector.cs
    World/        WorldArea.cs, OfferGenerator.cs, TravelEstimator.cs, CaptiveTracker.cs
    Squad/        SquadEvaluator.cs, FoodPlanner.cs, RideFinder.cs, Departure.cs, JobDriver_ExpeditionDepart.cs
    Resolve/      Resolver.cs, LootRoller.cs, InjuryMaker.cs, Arrival.cs
    Prospect/     ProspectCatalog.cs, ProspectService.cs, JobDriver_Prospect.cs, CompExpeditionsConsole.cs
    UI/           MapBaker.cs, BiomeColors.cs, ExpeditionsLook.cs, Window_Expeditions.cs (+ partials per tab),
                  Window_ExpeditionReport.cs
    Debug/        ExpeditionsDebug.cs
    Tests/        Expeditions.Tests.csproj, *Tests.cs
    UIRethemedCompat/ Expeditions.UIRethemed.csproj, LfsExpeditionsLook.cs
```

### Task 1: Scaffold the mod and the Logic test project

**Files:**
- Create: `Mods/Expeditions/About/About.xml`, `LoadFolders.xml`, `Languages/English/Keyed/Expeditions.xml` (empty `LanguageData`), `Source/Expeditions.csproj`, `Source/Core/ExpeditionsMod.cs`, `Source/Tests/Expeditions.Tests.csproj`, `Source/Tests/SmokeTests.cs`, `Source/Logic/Enums.cs`, `.gitignore` (`Source/**/obj/`, `Source/**/bin/`)
- Move: `BeyondOurReach/openspec/expeditions-mod-design.md` and `expeditions-mod-plan.md` to `Mods/Expeditions/openspec/`

**Interfaces:**
- Produces: `enum CombineRule { Sum, Min, Max, Average }`, `enum Outcome { Success, Partial, Failure }`, `enum PawnFate { Unharmed, Injured, Killed, Captured }`, `enum Ride { Foot, DropPods, Shuttle, Vehicles }`, `enum MapStyle { Vanilla, Holo }`, `enum Phase { Departing, Outbound, OnSite, Returning }`, all in `Logic/Enums.cs`. `ExpeditionsMod : Mod` with `static ExpeditionsSettings Settings`, and `ExpeditionsSettings : ModSettings { MapStyle style = Vanilla; int range = 60; int offerSlots = 5; }`. Harmony id `bambaryla.expeditions`, `PatchAll` in the `ExpeditionsMod` constructor.

- [ ] **Step 1:** `git init` in `Mods/Expeditions`. Write `About.xml` with name "Expeditions", packageId `bambaryla.expeditions`, author Bambaryła, `supportedVersions` 1.7, a Harmony dependency, `loadAfter` Harmony and `LazyFridayStudio.UIRethemed.Core`, and a short description from the spec's Intent. Write `LoadFolders.xml` like CommRecruiter's: `/`, `1.7`, and `Compat/UIRethemed/1.7` when `LazyFridayStudio.UIRethemed.Core` is active.
- [ ] **Step 2:** Write `Source/Expeditions.csproj`, copying CommRecruiter's csproj (net10.0, `RimWorldDir`, references to `Assembly-CSharp` and `UnityStubs` with `Private=False`, output `..\1.7\Assemblies\`), plus `Lib.Harmony` 2.4.3 with `ExcludeAssets="runtime"`, `Nullable enable`, and `<Compile Remove="Tests/**;UIRethemedCompat/**;obj/**" />`.
- [ ] **Step 3:** Write `Source/Tests/Expeditions.Tests.csproj` (net10.0, xunit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.12.0) with `<Compile Include="..\Logic\**\*.cs" LinkBase="Logic" />` and no game references. Write `SmokeTests.cs` with one test: `Assert.Equal(4, Enum.GetValues<Ride>().Length);`.
- [ ] **Step 4:** Run `dotnet test Source/Tests` and expect 1 passed. Run `dotnet build Source/Expeditions.csproj -c Release` and expect `1.7/Assemblies/Expeditions.dll`.
- [ ] **Step 5:** Move both openspec docs and the mockups. Commit: `Scaffold Expeditions mod and Logic tests`.

### Task 2: Scoring math (Curve, ChallengeMath, OutcomeRoll)

**Files:** Create `Source/Logic/Curve.cs`, `ChallengeMath.cs`, `OutcomeRoll.cs`. Test `Source/Tests/ChallengeMathTests.cs`, `OutcomeRollTests.cs`.

**Interfaces:**
- Produces:
  - `sealed class Curve(IReadOnlyList<(double x, double y)> points)` with `double Evaluate(double x)`: points sorted by x, linear between them, clamped to the end values.
  - `readonly record struct StatTerm(double Value, double Weight, double Reference)`.
  - `static class ChallengeMath`:
    - `const double TermCap = 2.0`.
    - `double PawnScore(IReadOnlyList<StatTerm> terms)` = `100 × Σ(w·min(v/ref, TermCap)) / Σw`; 0 when there are no terms or Σw is 0.
    - `double Combine(IReadOnlyList<double> scores, CombineRule rule)`; 0 when empty.
    - `double ChallengeChance(double score, double requirement, Curve ratioCurve)` = `ratioCurve.Evaluate(score/requirement)`, or 1 when requirement ≤ 0.
    - `double SuccessChance(IReadOnlyList<(double chance, double weight)> parts, double min = 0.05, double max = 0.95)`: weighted average, clamped; `max` when empty.
    - `double ArmedBonus(double baseBonus, double openness, double roll01, double spread)` = `base × openness × (1 + spread × (2·roll01 − 1))`.
    - `double StealthCap(double score, Ride ride, double loudCap)`: unchanged on Foot, otherwise `min(score, loudCap)`.
  - `readonly record struct FateWeights(double Unharmed, double Injured, double Killed, double Captured)`.
  - `static class OutcomeRoll` with `Outcome Roll(double successChance, double roll01, double partialBand = 0.15)`: Success if `roll < chance − band/2`, Partial if `roll < chance + band/2`, otherwise Failure. `PawnFate PickFate(FateWeights w, double roll01)`: cumulative in the order Unharmed, Injured, Killed, Captured, with Unharmed when the total is 0.

- [ ] **Step 1: Write the failing tests**

```csharp
static readonly Curve Ratio = new([(0, 0), (1, 0.6), (2, 0.95)]);
[Fact] public void Curve_interpolates_and_clamps() {
  Assert.Equal(0.3, Ratio.Evaluate(0.5), 9); Assert.Equal(0.775, Ratio.Evaluate(1.5), 9);
  Assert.Equal(0, Ratio.Evaluate(-1), 9); Assert.Equal(0.95, Ratio.Evaluate(3), 9); }
[Fact] public void PawnScore_averages_ratios() =>
  Assert.Equal(75, ChallengeMath.PawnScore([new(0.9, 1, 0.9), new(2, 1, 4)]), 9);
[Fact] public void PawnScore_caps_each_term() =>
  Assert.Equal(200, ChallengeMath.PawnScore([new(10, 1, 1)]), 9);
[Fact] public void Combine_rules() {
  double[] s = [50, 100, 75];
  Assert.Equal(225, ChallengeMath.Combine(s, CombineRule.Sum)); Assert.Equal(50, ChallengeMath.Combine(s, CombineRule.Min));
  Assert.Equal(100, ChallengeMath.Combine(s, CombineRule.Max)); Assert.Equal(75, ChallengeMath.Combine(s, CombineRule.Average));
  Assert.Equal(0, ChallengeMath.Combine([], CombineRule.Min)); }
[Fact] public void ChallengeChance_uses_ratio() {
  Assert.Equal(0.3, ChallengeMath.ChallengeChance(50, 100, Ratio), 9);
  Assert.Equal(1, ChallengeMath.ChallengeChance(50, 0, Ratio), 9); }
[Fact] public void SuccessChance_weights_and_clamps() {
  Assert.Equal(0.5, ChallengeMath.SuccessChance([(0.6, 2), (0.3, 1)]), 9);
  Assert.Equal(0.95, ChallengeMath.SuccessChance([(1.0, 1)]), 9); Assert.Equal(0.05, ChallengeMath.SuccessChance([(0, 1)]), 9);
  Assert.Equal(0.95, ChallengeMath.SuccessChance([]), 9); }
[Fact] public void ArmedBonus_spreads() {
  Assert.Equal(7.5, ChallengeMath.ArmedBonus(10, 0.5, 1.0, 0.5), 9); Assert.Equal(2.5, ChallengeMath.ArmedBonus(10, 0.5, 0.0, 0.5), 9); }
[Fact] public void Stealth_capped_when_loud() {
  Assert.Equal(90, ChallengeMath.StealthCap(90, Ride.Foot, 30)); Assert.Equal(30, ChallengeMath.StealthCap(90, Ride.Shuttle, 30)); }
// OutcomeRollTests
[Fact] public void Roll_bands() {
  Assert.Equal(Outcome.Success, OutcomeRoll.Roll(0.6, 0.5)); Assert.Equal(Outcome.Partial, OutcomeRoll.Roll(0.6, 0.55));
  Assert.Equal(Outcome.Failure, OutcomeRoll.Roll(0.6, 0.7)); }
[Fact] public void PickFate_cumulative() {
  var w = new FateWeights(1, 1, 1, 1);
  Assert.Equal(PawnFate.Unharmed, OutcomeRoll.PickFate(w, 0.1)); Assert.Equal(PawnFate.Injured, OutcomeRoll.PickFate(w, 0.3));
  Assert.Equal(PawnFate.Killed, OutcomeRoll.PickFate(w, 0.6)); Assert.Equal(PawnFate.Captured, OutcomeRoll.PickFate(w, 0.9));
  Assert.Equal(PawnFate.Unharmed, OutcomeRoll.PickFate(new(0, 0, 0, 0), 0.9)); }
```

- [ ] **Step 2:** Run `dotnet test Source/Tests` and expect a compile failure (the types don't exist yet).
- [ ] **Step 3:** Implement the three files to the signatures above.
- [ ] **Step 4:** Run `dotnet test Source/Tests` and expect everything to pass.
- [ ] **Step 5:** Commit: `Add challenge and outcome math`.

### Task 3: Prospecting, food, loot, travel and projection math

**Files:** Create `Source/Logic/LeadChance.cs`, `ProspectSeed.cs`, `FoodMath.cs`, `LootFit.cs`, `TravelMath.cs`, `Projection.cs`. Tests: one `*Tests.cs` per file.

**Interfaces:**
- Consumes: `Curve`, `Ride` (Tasks 1-2).
- Produces:
  - `LeadChance.Of(int techLevel, double marketValue, Curve techCurve, Curve valueCurve)` = `tech(techLevel) × value(marketValue)`, clamped to [0.01, 0.99]. `techLevel` is the int value of vanilla `TechLevel`: Undefined 0, Animal 1, Neolithic 2, Medieval 3, Industrial 4, Spacer 5, Ultra 6, Archotech 7.
  - `ProspectSeed`:
    - `const int WindowTicks = 150000`.
    - `int WindowAt(long ticksAbs)`.
    - `long TicksUntilNextWindow(long ticksAbs)`.
    - `double Roll01(int worldSeed, int window, string key)`: 32-bit FNV-1a over the UTF-8 bytes of `$"{worldSeed}|{window}|{key}"`, divided by `uint.MaxValue`. Never `string.GetHashCode`, which is randomized per process.
    - `bool HasLead(int worldSeed, int window, string key, double chance)` = `Roll01 < chance`.
  - `FoodMath.Required(IReadOnlyList<double> dailyNutrition, double tripDays)` and `FoodMath.Shortfall(double required, double available)` (never below 0).
  - `readonly record struct LootStack(double UnitValue, double UnitMass, int Count)`. `LootFit.Fit(IReadOnlyList<LootStack> stacks, double capacityKg) → int[]` gives the kept count per input index. Massless stacks are always kept whole. The rest go by value per kilogram, highest first, filling partially until capacity runs out.
  - `readonly record struct TravelTuning(double FlightBaseDays = 0.25, double FlightDaysPerTile = 0.01, double VehicleFootFactor = 0.5)`, with:
    - `TravelMath.OutDays(Ride, double footDays, double tiles, TravelTuning)`
    - `TravelMath.BackDays(...)` (same parameters)
    - `TravelMath.TripDays(Ride, double footDays, double tiles, double siteDays, TravelTuning)`
    - `TravelMath.IsLoud(Ride)`

    Foot: walking both ways. DropPods: flight out, walking back. Shuttle: flight both ways. Vehicles: `footDays × VehicleFootFactor` both ways. Flight = `FlightBaseDays + FlightDaysPerTile × tiles`.
  - `readonly record struct Vec3(double X, double Y, double Z)` and `Projection.AzimuthalEquidistant(Vec3 center, Vec3 point, Vec3 north) → (double x, double y)` in radians of arc:

    ```
    c = norm(center); d = acos(clamp(dot(c,p), -1, 1)); if d < 1e-12 return (0,0)
    t = norm(p - c*dot(c,p)); nT = norm(north - c*dot(c,north)); e = cross(nT, c)
    return (d*dot(t,e), d*dot(t,nT))
    ```

- [ ] **Step 1: Write the failing tests**

```csharp
static readonly Curve Tech = new([(0, 1), (4, 0.8), (5, 0.45), (6, 0.2), (7, 0.05)]);
static readonly Curve Value = new([(0, 1), (50, 0.85), (500, 0.55), (2000, 0.3), (10000, 0.1)]);
[Fact] public void Lead_multiplies_and_clamps() {
  Assert.Equal(0.68, LeadChance.Of(4, 50, Tech, Value), 9);
  Assert.Equal(0.01, LeadChance.Of(7, 10000, Tech, Value), 9); Assert.Equal(0.99, LeadChance.Of(0, 0, Tech, Value), 9); }
[Fact] public void Seed_windows() {
  Assert.Equal(0, ProspectSeed.WindowAt(0)); Assert.Equal(0, ProspectSeed.WindowAt(149999)); Assert.Equal(1, ProspectSeed.WindowAt(150000));
  Assert.Equal(50000, ProspectSeed.TicksUntilNextWindow(100000)); }
[Fact] public void Seed_is_deterministic_and_fair() {
  Assert.Equal(ProspectSeed.Roll01(7, 3, "Steel"), ProspectSeed.Roll01(7, 3, "Steel"));
  Assert.True(ProspectSeed.HasLead(7, 3, "Steel", 1.0)); Assert.False(ProspectSeed.HasLead(7, 3, "Steel", 0.0));
  int hits = Enumerable.Range(0, 2000).Count(i => ProspectSeed.HasLead(7, 3, "Item" + i, 0.3));
  Assert.InRange(hits, 500, 700);
  Assert.Contains(Enumerable.Range(0, 50), i => ProspectSeed.HasLead(7, 3, "K" + i, 0.5) != ProspectSeed.HasLead(7, 4, "K" + i, 0.5)); }
[Fact] public void Food_required_and_shortfall() {
  Assert.Equal(28.0, FoodMath.Required([1.6, 1.6, 0.8], 7), 9);
  Assert.Equal(8, FoodMath.Shortfall(28, 20), 9); Assert.Equal(0, FoodMath.Shortfall(28, 30), 9); }
[Fact] public void Loot_keeps_value_per_kg() {
  LootStack[] s = [new(10, 1, 10), new(100, 20, 3), new(5, 0, 50)];
  Assert.Equal([10, 0, 50], LootFit.Fit(s, 25)); Assert.Equal([10, 2, 50], LootFit.Fit(s, 50)); Assert.Equal([0, 0, 50], LootFit.Fit(s, 0)); }
[Fact] public void Travel_by_ride() {
  var t = new TravelTuning();
  Assert.Equal(6.5, TravelMath.TripDays(Ride.Foot, 3, 30, 0.5, t), 9);  Assert.Equal(4.05, TravelMath.TripDays(Ride.DropPods, 3, 30, 0.5, t), 9);
  Assert.Equal(1.6, TravelMath.TripDays(Ride.Shuttle, 3, 30, 0.5, t), 9); Assert.Equal(3.5, TravelMath.TripDays(Ride.Vehicles, 3, 30, 0.5, t), 9);
  Assert.False(TravelMath.IsLoud(Ride.Foot)); Assert.True(TravelMath.IsLoud(Ride.DropPods)); }
[Fact] public void Projection_axes() {
  Vec3 c = new(1, 0, 0), n = new(0, 1, 0); double a = 0.1;
  Assert.Equal((0.0, 0.0), Projection.AzimuthalEquidistant(c, c, n));
  var up = Projection.AzimuthalEquidistant(c, new(Math.Cos(a), Math.Sin(a), 0), n); Assert.Equal(0, up.x, 9); Assert.Equal(a, up.y, 9);
  var side = Projection.AzimuthalEquidistant(c, new(Math.Cos(a), 0, -Math.Sin(a)), n); Assert.Equal(a, side.x, 9); Assert.Equal(0, side.y, 9); }
```

- [ ] **Step 2:** Run `dotnet test Source/Tests` and expect a compile failure.
- [ ] **Step 3:** Implement the six files to the signatures above.
- [ ] **Step 4:** Run `dotnet test Source/Tests` and expect everything to pass.
- [ ] **Step 5:** Commit: `Add prospect, food, loot, travel and projection math`.

### Task 4: Defs and shipped content

**Files:** Create `Source/Defs/*.cs` and `Defs/Expeditions_Challenges.xml`, `Expeditions_Sites.xml`, `Expeditions_Tuning.xml`, `Expeditions_Jobs.xml` (the job defs for Tasks 8-9).

**Interfaces:**
- Consumes: Logic types.
- Produces:
  - `ExpeditionChallengeDef : Def { List<ChallengeStat> stats; CombineRule combine; bool isStealth; bool isHaul; bool isSearch; List<SkillDef> skills; }`, where `ChallengeStat { StatDef stat; float weight = 1; float reference = 1; }`.
  - `ExpeditionSiteDef : Def`:

    ```
    string iconPath; Color color = white;
    List<BiomeDef> biomes;            // empty = any land biome
    List<Hilliness> hilliness;        // empty = any
    bool needsRiver, needsRoad, needsCoast;
    FactionRelationKind? needsFaction;  // a non-hidden faction with that relation and a settlement in range
    List<SiteChallenge> challenges;   // SiteChallenge { ExpeditionChallengeDef challenge; float weight = 1; float requirement = 100; }
    List<LootEntry> loot;             // LootEntry { ThingDef thing; ThingCategoryDef category; IntRange count; float chance = 1; }
    float findChanceBase = 0.6f;
    RiskProfile risk;                 // RiskProfile { FateEntry success, partial, failure; }, FateEntry { float unharmed, injured, killed, captured; } → ToWeights()
    float daysOnSite = 0.5f; float openness = 1f; IntRange squadSize = new(1, 6);
    List<ThingCategoryDef> servesCategories; bool rescueOnly; bool fallback;
    int goodwillReward; int silverReward;
    ```

    `ConfigErrors` reports: no challenges, a null challenge, `squadSize.min < 1`, and a `servesCategories` entry that is missing.
  - `ExpeditionExclusionDef : Def { List<ThingDef> things; List<ThingCategoryDef> categories; }`.
  - `ExpeditionTuningDef : Def` (single instance `Expeditions_Tuning`):

    ```
    SimpleCurve challengeRatioCurve, techLevelCurve, valueCurve; float partialBand = 0.15f;
    IntRange offerIntervalTicks = 60000~180000, offerLifetimeTicks = 180000~480000, prospectLifetimeTicks = 300000~480000;
    float loudStealthCap = 30; float armedFightBonus = 60; float armedSpread = 0.5f;
    float flightBaseDays = 0.25f, flightDaysPerTile = 0.01f, vehicleFootFactor = 0.5f;
    int prospectBaseTicks = 15000; float xpPerChallengeWeight = 800;
    List<BiomeColor> biomeColors;     // BiomeColor { BiomeDef biome; Color color; }
    ```

    It has helpers `Curve RatioCurve`, `Curve TechCurve`, `Curve ValueCurve` (converted from the `SimpleCurve` points), and `TravelTuning Travel`.
  - `[DefOf] ExpeditionsDefOf { ExpeditionTuningDef Expeditions_Tuning; ExpeditionChallengeDef Exp_Fight, Exp_Stealth, Exp_Haul; ExpeditionSiteDef Exp_HoldingCamp, Exp_Rumor; JobDef Exp_Prospect, Exp_Depart; }`.
- Shipped XML (the numbers are starting points):
  - **Challenges:**
    - `Exp_Fight` (Sum): ShootingAccuracyPawn ref 0.9, MeleeDPS ref 4, ArmorRating_Sharp ref 0.6, ArmorRating_Blunt ref 0.3. Skills Shooting, Melee.
    - `Exp_Stealth` (Min, isStealth): HuntingStealth ref 0.5, MoveSpeed ref 4.6.
    - `Exp_SearchOre` (Max, isSearch): MiningSpeed ref 1, MiningYield ref 1. Skill Mining.
    - `Exp_SearchSalvage` (Max, isSearch): ResearchSpeed ref 1, ConstructionSpeed ref 1. Skills Intellectual, Construction.
    - `Exp_SearchGame` (Max, isSearch): HuntingStealth ref 0.5, ShootingAccuracyPawn ref 0.9. Skill Animals.
    - `Exp_Haul` (Sum, isHaul): CarryingCapacity ref 75.
    - `Exp_Endure` (Average): ImmunityGainSpeed ref 1, MedicalTendQuality ref 1, ToxicResistance ref 0.5 at weight 0.5. Skill Medicine.
  - **Sites:**
    - `Exp_AbandonedMine` (hilliness LargeHills or Mountainous; SearchOre 2, Endure 1, Haul 1.5, Fight 0.5; ore loot; serves ResourcesRaw, StoneChunks)
    - `Exp_CrashedShip` (SearchSalvage 2, Endure 1, Haul 1; components and plasteel; serves Manufactured, Weapons)
    - `Exp_HostileOutpost` (needsFaction Hostile; Fight 2, Stealth 1.5, Haul 1; weapons, apparel, medicine, silver; serves Weapons, Apparel)
    - `Exp_GameHerd` (biomes TemperateForest, BorealForest, AridShrubland, Tundra; SearchGame 2, Haul 2, Fight 0.5; meat and leather; serves MeatRaw, Leathers)
    - `Exp_StalledConvoy` (needsRoad; SearchSalvage 1, Haul 2, Fight 0.5; textiles, food, medicine; serves Textiles, Foods, Medicine)
    - `Exp_RiverCache` (needsRiver; SearchSalvage 1.5, Endure 1; drugs, silver, components; serves Drugs)
    - `Exp_FriendlyJob` (needsFaction Ally; Fight 1.5, Endure 1; goodwillReward 8, silverReward 400)
    - `Exp_HoldingCamp` (rescueOnly; Fight 1.5, Stealth 1.5)
    - `Exp_Rumor` (fallback; SearchSalvage 1, Endure 1, Fight 1)

    Risk defaults are success (0.8, 0.2, 0, 0), partial (0.5, 0.4, 0.07, 0.03) and failure (0.1, 0.5, 0.2, 0.2). `Exp_HostileOutpost` and `Exp_HoldingCamp` double the killed and captured weights.
  - **Tuning curves:** ratio (0,0) (0.5,0.2) (1,0.6) (1.5,0.85) (2,0.95). Tech and value as in Task 3's tests. Biome colors for the vanilla and DLC biomes, picked by eye from the world map.

- [ ] **Step 1:** Write the def classes and `ExpeditionsDefOf`. In each def's `ConfigErrors`, report the problems listed above.
- [ ] **Step 2:** Write the XML.
- [ ] **Step 3:** Build: `dotnet build Source/Expeditions.csproj -c Release`. Expect 0 errors.
- [ ] **Step 4:** Enable the mod and run `RimWorldWin64.exe -quicktest`. Then check `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log` with `grep -n "Exp_\|Expeditions" Player.log`. Expect no config errors and no XML errors.
- [ ] **Step 5:** Commit: `Add expedition defs and shipped content`.

### Task 5: Squad evaluation (stats to scores, with breakdowns)

**Files:** Create `Source/Squad/SquadEvaluator.cs`.

**Interfaces:**
- Consumes: defs (Task 4) and `ChallengeMath` (Task 2).
- Produces:
  - `record Contribution(Pawn pawn, StatDef stat, float value, string explanation)`. The explanation is the stat's `GetExplanationFull` trimmed to its non-zero lines, so it names gear, bionics and traits.
  - `record ChallengeResult(SiteChallenge site, double score, double chance, List<Contribution> parts)`.
  - `record SquadEvaluation(List<ChallengeResult> challenges, double success, double find)`.
  - `record RideOption(Ride ride, bool available, string? reason, float cargoKg, int armedCount, List<Thing> things)`, defined here and filled in by `RideFinder` in Task 9.
  - `static SquadEvaluation SquadEvaluator.Evaluate(IReadOnlyList<Pawn> pawns, ExpeditionSiteDef site, Ride ride, RideOption? rideOption, int seed)`:
    - Each pawn's score is `ChallengeMath.PawnScore` over `pawn.GetStatValue(stat)`, and the squad's comes from `Combine` with the challenge's rule. Animals in the squad (pack animals) count only toward Haul.
    - Haul adds `rideOption.cargoKg / 75 × 100` for every ride except Foot and DropPods.
    - Stealth goes through `StealthCap` with `loudStealthCap`.
    - Fight adds `ArmedBonus(armedFightBonus × armed vehicle count, site.openness, Roll01(seed, 0, "armed"), armedSpread)` for Vehicles.
    - `find = clamp(site.findChanceBase × (0.5 + 0.5 × the best Search challenge's chance), 0.05, 0.95)`, or `findChanceBase` when the site has no Search challenge.
    - Stats a pawn can't have (`StatWorker.IsDisabledFor`) count as 0.
  - The caller passes `seed`. From Task 6 on, that's `Opportunity.Seed`. The dev action in Step 2 passes 0.

- [ ] **Step 1:** Implement the evaluator.
- [ ] **Step 2:** Add a dev action, "Expeditions: evaluate selected pawns against site...". It opens a float menu of site defs and logs every challenge's score, chance and contributions.
- [ ] **Step 3:** Check it in game. For `Exp_HostileOutpost`, a pawn in power armor and a pawn in a parka should show Fight higher for the armored pawn. Stealth for the pair should follow the lower of the two pawns. Each contribution line should name the gear.
- [ ] **Step 4:** Commit: `Add squad evaluation`.

### Task 6: Game state, the holder and saving

**Files:** Create `Source/Core/ExpeditionsGame.cs`, `Opportunity.cs`, `Expedition.cs`, `ExpeditionReport.cs`.

**Interfaces:**
- Produces:
  - `class Opportunity : IExposable`:

    ```
    int id; ExpeditionSiteDef site; PlanetTile tile; int expiresTick;
    bool prospected; ThingDef? target; int targetCount;
    Faction? faction; Pawn? captive;
    int Seed => id * 7919 + Find.World.info.Seed;
    ```

  - `class Expedition : IExposable, IThingHolder`:

    ```
    int id; Opportunity opp; Ride ride; Phase phase;
    List<Pawn> squad; List<Thing> rideThings; ThingOwner<Thing> contents;   // away pawns, animals, vehicles, shuttle and cargo
    Map? originMap; PlanetTile originTile;
    int phaseEndTick; double tripFootDays; float foodNeeded;
    ExpeditionReport? report; List<Thing> pendingFood;
    ```

    `IEnumerable<Pawn> AwayPawns` lists the pawns in `contents`. `ParentHolder` is null.
  - `class ExpeditionReport : IExposable`:

    ```
    string siteLabel; Outcome outcome; bool found; int tick;
    List<PawnResult> pawns; List<ThingDefCountClass> loot, lostToHaul;
    // PawnResult { Pawn pawn; PawnFate fate; List<string> injuries; Dictionary<SkillDef,float> xp; }
    ```

  - `class ExpeditionsGame : GameComponent, IThingHolder`:

    ```
    static ExpeditionsGame Instance;
    List<Opportunity> offers; List<Expedition> active; List<ExpeditionReport> history;   // history capped at 20
    Dictionary<ThingDef,int> prospectTicksDone; HashSet<ThingDef> prospectedThisWindow; int prospectWindow;
    Dictionary<Pawn,int> capturedTick; int nextOfferTick; int nextId;

    int NextId(); bool IsAway(Pawn p); bool IsCommitted(Pawn p);   // in any expedition's squad, Departing included
    IEnumerable<Pawn> AllAwayPawns;
    event-like hooks for Tasks 8-10: void Tick250();
    ```

    `GetChildHolders` returns the active expeditions. `GameComponentTick` calls `Tick250` every 250 ticks. Phase flow: Departing (until everyone has boarded) → Outbound (until `phaseEndTick`) → OnSite (for `daysOnSite`, then `Resolver.Resolve`) → Returning (until `phaseEndTick`, then `Arrival.Return`). Tasks 8-10 fill in the phase handlers: `Departure.Tick(e)`, `Resolver.Resolve(e)`, `Arrival.Return(e)`, `OfferGenerator.Tick()`.
- Saving: Scribe `contents` deep, everything else by value or by reference. Pawns killed or captured are passed to world pawns before they leave `contents` (Task 10), so their references stay valid in reports.

- [ ] **Step 1:** Implement the classes, with `ExposeData` and a `PostLoadInit` pass that drops null references.
- [ ] **Step 2:** Add dev actions "Expeditions: send selected pawns away (test)" and "Expeditions: bring all back (test)". They despawn the selected colonists into a test `Expedition` (phase Outbound, opportunity `Exp_Rumor` on the home tile) and spawn them back near the map center.
- [ ] **Step 3:** Check in game: send two pawns away, save, load, bring them back. They come back with the same gear, health and needs as when they left. Their needs didn't move while they were away, because nothing ticks them.
- [ ] **Step 4:** Commit: `Add expedition state and holder`.

### Task 7: Keep away pawns counted as colonists

**Files:** Create `Source/Core/HolderPatches.cs`.

**Interfaces:**
- Consumes: `ExpeditionsGame.AllAwayPawns`.
- Produces these Harmony patches:
  - Postfix on the `PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead` getter: `__result.AddRange(AllAwayPawns)`. Every derived list builds on it, as of 1.7.
  - Prefix on `GameEnder.CheckOrUpdateGameOver`: if any away pawn is an alive free colonist, set `__instance.gameEnding = false` and return false.
  - On `ColonistBar.CheckRecacheEntries`, a prefix stores `entriesDirty` in `__state`. A postfix, when `__state` is true, appends `new ColonistBar.Entry(pawn, null, group)` for each away colonist, with one new group per expedition after the existing groups.
  - `ExpeditionsGame` calls `Find.ColonistBar.MarkColonistsDirty()` whenever pawns enter or leave the holder.

- [ ] **Step 1:** Implement the patches.
- [ ] **Step 2:** Check in game with the Task 6 dev action:
  - Send every colonist away and wait one in-game day: the game does not end.
  - The colonist bar shows the away pawns in their own group, and clicking one doesn't throw.
  - The pawns count toward the colonist count in the world tab and in wealth.
- [ ] **Step 3, review focus 4 smoke checklist:** with a pawn away, open the colonist bar tooltip, the pawn's info card from the colonist bar, the Ideology roles window, the Factions tab, the Assign tab, the History tab, and the Caravan formation dialog on the home map. Expect no errors in the log. Fix anything that throws, usually with a null guard in a small patch, and list it in `HolderPatches.cs`.
- [ ] **Step 4:** Commit: `Keep away pawns counted as colonists`.

### Task 8: World area, offers, travel estimates and prospecting

**Files:** Create `Source/World/WorldArea.cs`, `OfferGenerator.cs`, `TravelEstimator.cs`, `Source/Prospect/ProspectCatalog.cs`, `ProspectService.cs`, `JobDriver_Prospect.cs`, `CompExpeditionsConsole.cs`, `Source/Core/DefInjector.cs`.

**Interfaces:**
- Produces:
  - `static List<PlanetTile> WorldArea.TilesInRange(PlanetTile home, int range)`: breadth-first over `Find.WorldGrid.GetTileNeighbors`, surface layer only, land tiles only (`!tile.WaterCovered`), cached per `(home, range)` until `ExpeditionsGame` clears it.
  - `static bool OfferGenerator.SiteFits(ExpeditionSiteDef, PlanetTile, out Faction? faction)` and `static Opportunity? OfferGenerator.TryMake(ExpeditionSiteDef?, PlanetTile home, ThingDef? target)`.
  - `static void OfferGenerator.Tick()`: expire offers whose `expiresTick` has passed. While there are fewer than `Settings.offerSlots` ready-made offers (prospected ones don't count) and `nextOfferTick` has passed, make one from a random non-rescue, non-fallback site that fits a random tile in range, and reroll `nextOfferTick` from `offerIntervalTicks`.
  - `static int TravelEstimator.FootTicks(PlanetTile from, PlanetTile to, List<Pawn> pawns, float massUsage, float massCapacity)`:

    ```
    path = from.Layer.Pather.FindPath(from, to, null)   // -1 when !path.Found
    ticksPerMove = CaravanTicksPerMoveUtility.GetTicksPerMove(pawns, massUsage, massCapacity, false, null)
    CaravanArrivalTimeEstimator.EstimatedTicksToArrive(from, to, path, 0f, ticksPerMove, Find.TickManager.TicksAbs)
    path.ReleaseToPool()
    ```

    `TravelEstimator.NominalFootDays(PlanetTile from, PlanetTile to)` uses `CaravanTicksPerMoveUtility.DefaultTicksPerMove`, for the list row.
  - `static bool ProspectCatalog.Eligible(ThingDef def)`: `category == Item`, `EverHaulable`, not a corpse, `tradeability != None`, `BaseMarketValue > 0`, `!destroyOnDrop`, and not in any `ExpeditionExclusionDef` (as a thing, or in a category or its parents). `static IReadOnlyList<ThingDef> ProspectCatalog.All` is cached and sorted by label. `static double ProspectCatalog.Chance(ThingDef)` = `LeadChance.Of((int)def.techLevel, def.BaseMarketValue, …)`.
  - `ProspectService`:
    - `bool CanStart(ThingDef, out string reason)`: false with `Exp_NoNewInfo` when the item was already prospected this window.
    - `int TotalTicks(Pawn)` = `prospectBaseTicks / max(0.2, stat)`, where the stat is `NegotiationAbility`, or `ResearchSpeed` when Social is disabled.
    - `void Complete(ThingDef, Map consoleMap)`: rolls `HasLead(Find.World.info.Seed, window, def.defName, Chance)`. On a lead, it makes a prospected opportunity from a site whose `servesCategories` contains one of the item's categories or their parents, falling back to `Exp_Rumor`. `targetCount` = `max(1, round(800 / BaseMarketValue))`, and the expiry comes from `prospectLifetimeTicks`. It sends a letter, `Exp_LeadFound` or `Exp_NoLead`. It also refreshes `prospectWindow` and clears the set when the window changes.
  - `JobDriver_Prospect`: target A is the console. Go to its interaction cell, then a toil that advances `prospectTicksDone[def]`, with a progress bar, and calls `Complete` when the total is reached. The job's `plantDefToSow` field carries the item def, as vanilla's job fields allow. It fails if the console is unpowered (`!CanUseCommsNow`).
  - `CompExpeditionsConsole : ThingComp`: an "Expeditions" gizmo that opens `Window_Expeditions(consoleThing)`. `[StaticConstructorOnStartup] DefInjector` adds it to every `ThingDef` whose `thingClass` is assignable to `Building_CommsConsole`, as CommRecruiter does.

- [ ] **Step 1:** Implement `WorldArea`, `OfferGenerator` and `TravelEstimator`.
- [ ] **Step 2:** Implement the prospect classes, the console comp and `DefInjector`.
- [ ] **Step 3:** Add dev actions "Expeditions: spawn offer" and "Expeditions: finish prospect for <def>" (a float menu over `ProspectCatalog.All`).
- [ ] **Step 4:** Check in game:
  - Offers appear within range, and a mine only appears on hilly tiles.
  - Prospecting Steel twice in one window: the second attempt is refused with "No new information available yet" before the job starts.
  - Interrupting the job by drafting the pawn and restarting it with another colonist continues the progress bar.
  - After the window changes, the item can be asked again.
- [ ] **Step 5:** Commit: `Add offers, travel estimates and prospecting`.

### Task 9: Rides, food and departure

**Files:** Create `Source/Squad/RideFinder.cs`, `FoodPlanner.cs`, `Departure.cs`, `JobDriver_ExpeditionDepart.cs`.

**Interfaces:**
- Consumes: Tasks 6 and 8.
- Produces:
  - The squad is colonists plus, optionally, colony pack animals (`RaceProps.packAnimal`). The picker in Task 12 lists both, and animals count only toward Haul and food.
  - `static List<RideOption> RideFinder.For(Map map, List<Pawn> squad, int tiles)` (`RideOption` comes from Task 5), one per `Ride`:
    - **Foot:** always available.
    - **DropPods:** a group of `CompTransporter` and `CompLaunchable` buildings without `CompShuttle`. Total `MassCapacity` ≥ the squad's mass, and `MaxLaunchDistanceAtFuelLevel(MinFuelLevelInGroup)` ≥ tiles.
    - **Shuttle:** a spawned player-owned thing with `CompShuttle`, or with `CompLaunchable` whose def is a shuttle (check how Odyssey's passenger shuttle is identified in 1.7 and record the rule in a comment). Its fuel must cover twice the tiles.
    - **Vehicles:** only when a type named `Vehicles.VehiclePawn` loads (`GenTypes.GetTypeInAnyAssembly`). Use reflection for seats and cargo. Check the member names against the installed Vehicle Framework assembly and keep them in one `VehicleCompat` nested class. If any lookup fails, log one warning and treat Vehicles as unavailable (review focus 5).
  - `FoodPlanner`:
    - `float DailyNeed(Pawn p)` = `p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) × 60000`, or 0 without a food need.
    - `float Available(List<Pawn> squad, Map map, out List<(Thing thing, int count)> pickups)`: inventory food first (`FoodUtility.WillEat` for any member), then the nearest reachable unforbidden stockpile food until the requirement is met.
    - `void ConsumeOnReturn(List<Pawn> returning, float nutrition)`: removes the lowest-value food first.
  - `static AcceptanceReport Departure.CanLaunch(Opportunity, List<Pawn>, RideOption, Map)` checks: the console's map has a powered comms console, the squad size is within `site.squadSize`, no pawn is committed elsewhere, the trip is reachable on foot for Foot and Vehicles, the ride is available, and the food shortfall is 0.
  - `static Expedition Departure.Launch(Opportunity, List<Pawn>, RideOption, Map)`:
    - Creates the expedition in `Phase.Departing` and removes the opportunity from `offers`.
    - Gives each pawn its pickups (`JobDefOf.TakeInventory`), then `Exp_Depart`.
    - `JobDriver_ExpeditionDepart`: on Foot and Vehicles it goes to an exit cell from `RCellFinder.TryFindBestExitSpot`; on DropPods and Shuttle it goes to the ride's position. On arrival it calls `Departure.Board(pawn, expeditionId)`, which despawns the pawn (with its carried things) into `contents`. Expedition ids are saved on the job (`job.count`), so a load mid-departure resumes (review focus 3).
  - `static void Departure.Tick(Expedition e)` while Departing:
    - Drops squad members who are downed, dead, mentally broken, drafted, or no longer on the map. If the squad falls below `squadSize.min`, it cancels: board-ins are spawned back at the exit spot, nothing is consumed, and the opportunity goes back on the board (review focus 1).
    - When everyone has boarded:
      - DropPods: destroy the pods and consume the launcher fuel for `tiles`, with a leaving skyfaller for looks.
      - Shuttle and Vehicles: move them into `contents`.
      - Then: pack animals that were in the squad go with them, `phase = Outbound`, and `phaseEndTick` comes from `TravelMath.OutDays`.

- [ ] **Step 1:** Implement `RideFinder` and `FoodPlanner`.
- [ ] **Step 2:** Implement `Departure` and the job driver.
- [ ] **Step 3:** Check in game for each ride: Foot, DropPods (pods consumed), Shuttle if available, and Vehicles with VF if installed. Then the edge cases:
  - Draft a pawn mid-departure in a squad of 2 with a minimum of 2: the expedition is cancelled, food stays, the offer is back.
  - Save and load mid-departure: the squad keeps walking out.
  - Without VF: no errors in the log.
- [ ] **Step 4:** Commit: `Add rides, food and departure`.

### Task 10: Resolution, return and captives

**Files:** Create `Source/Resolve/Resolver.cs`, `LootRoller.cs`, `InjuryMaker.cs`, `Arrival.cs`, `Source/World/CaptiveTracker.cs`.

**Interfaces:**
- Consumes: Tasks 2, 5, 6 and 9.
- Produces:
  - `Resolver.Resolve(Expedition e)` runs at the end of `Outbound` plus `daysOnSite` (the `OnSite` phase):
    1. Evaluate the squad.
    2. Roll the outcome (`OutcomeRoll.Roll`, `partialBand`) and the find (`Rand.Value < find`), seeded with `Rand.PushState(opp.Seed)`.
    3. Loot: `LootRoller.Roll(e)`.
    4. For each pawn, `PickFate` from the site's risk for the outcome, then apply it:
       - **Injured:** `InjuryMaker.Apply(pawn, severity: 0.3 to 0.7, allowPartLoss: outcome == Failure)`.
       - **Killed:** remove from `contents`, `Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever)`, `pawn.Kill(null)`, then send a death letter at once.
       - **Captured:** remove from `contents`, then `captor.kidnapped.Kidnap(pawn, null)`. The captor is `opp.faction`, or a random hostile, non-hidden faction. Check `Kidnap` with a null kidnapper in the decompiled source first; if it needs a pawn, pass one of the captor's world pawns or generate one. Record `capturedTick[pawn]`.
    5. Gear: each worn apparel and equipment loses `HitPoints × {0.05, 0.2, 0.35}` for Unharmed, Injured and Captured-or-Killed.
    6. XP: `pawn.skills.Learn(skill, xpPerChallengeWeight × siteChallenge.weight, direct: true)` for each challenge skill, survivors only.
    7. Rewards: goodwill and silver on Success, half on Partial.
    8. Then `phase = Returning`, with `phaseEndTick` from `TravelMath.BackDays`.
  - `static List<Thing> LootRoller.Roll(Expedition e)`:
    - Failure or not found gives an empty list. Partial halves every count.
    - Prospected opportunities always include `target × targetCount` when found.
    - Category entries pick a random eligible def from the category, limited to the faction's tech level for faction sites.
    - Then `LootFit.Fit` with the capacity: pawns' `MassCapacity` minus their gear, plus the ride's `cargoKg` for Shuttle and Vehicles. Pods walk back, so pods add nothing.
    - What doesn't fit is recorded in `report.lostToHaul` and destroyed. What fits goes into `contents`.
  - `static void InjuryMaker.Apply(Pawn pawn, float severity01, bool allowPartLoss)`: adds `Gunshot`, `Cut` and `Bruise` hediffs to random non-vital outer parts until the summed injury severity reaches `severity01 × the pawn's summed part health × 0.5`. It never takes a part below 1 HP unless `allowPartLoss`, and never makes `ShouldBeDead` true.
  - `Arrival.Return(Expedition e)` at the end of Returning:
    - The target map is `originMap` if it is still a player home, otherwise the first player home map. With none, it makes a vanilla caravan on `originTile` with `CaravanMaker.MakeCaravan` (review focus 2).
    - Foot, Pods and Vehicles: spawn at an edge cell from `CellFinder.TryFindRandomEdgeCellWith` (standable, can reach the colony). Shuttle: spawn the shuttle at a roofless cell near the console with a drop effect, then the pawns next to it.
    - Then `FoodPlanner.ConsumeOnReturn(returning, foodNeeded)`. Cargo is placed next to the pawns. Push the report to `history` (capped at 20), send a letter with an "Open report" choice, `MarkColonistsDirty`, and remove the expedition.
  - `CaptiveTracker`:
    - `record CaptiveInfo(Pawn pawn, Faction captor, int heldTicks)`.
    - `static IEnumerable<CaptiveInfo> All()`: every faction's `kidnapped` list (find its public reader in the decompiled `KidnappedPawnsTracker`), colonists only. `heldTicks` comes from `capturedTick`, which is set to now the first time a pawn is seen.
    - `static Opportunity CreateRescue(CaptiveInfo c, PlanetTile home)`: site `Exp_HoldingCamp`. The tile is the captor's nearest settlement within range, or a random tile in range when there is none (factions without settlements included). The rescue expires after `prospectLifetimeTicks`.
    - On a found Success: `captor.kidnapped.RemoveKidnappedPawn(pawn)`, `Find.WorldPawns.RemovePawn(pawn)`, restore the player faction if `Kidnap` changed it (check in the decompiled source), add the pawn to `contents`, and add a report line.

- [ ] **Step 1:** Implement `InjuryMaker` and `LootRoller`.
- [ ] **Step 2:** Implement `Resolver`, `Arrival` and `CaptiveTracker`.
- [ ] **Step 3:** Add a dev action "Expeditions: force next outcome" (Success, Partial or Failure) and "advance phase" on the selected expedition.
- [ ] **Step 4:** Check in game:
  - A forced Success returns with loot capped by carrying capacity, and the report shows the lost loot.
  - A forced Failure produces injuries, a death letter and a captive in the Factions tab, and the captive's rescue brings them home.
  - Abandon the origin map while a squad is away (dev: destroy map): they return to the other home map. With no home map, they appear as a caravan.
  - Save and load in each phase.
- [ ] **Step 5:** Commit: `Add resolution, return and captives`.

### Task 11: Map bake

**Files:** Create `Source/UI/MapBaker.cs`, `BiomeColors.cs`.

**Interfaces:**
- Consumes: `WorldArea.TilesInRange`, `Projection`, `ExpeditionTuningDef.biomeColors`.
- Produces:
  - `class BakedMap { Texture2D texture; float pxPerRadian; Vector2 ToPixel(PlanetTile); PlanetTile TileAt(Vector2 px); }`.
  - `static BakedMap MapBaker.Get(PlanetTile home, int range, MapStyle style)`: cached by `(home, range, style)`, cleared by `ExpeditionsGame` on world load and by the rebake dev action.
  - Bake, with a 1024×1024 RGBA32 texture:
    - Project every tile center with `AzimuthalEquidistant(GetTileCenter(home), GetTileCenter(t), layer.NorthPolePos)`. Scale so the farthest tile lands at 0.95 of the half size.
    - Fill each pixel from the nearest tile by splatting a disc per tile into a distance buffer.
    - **Vanilla:** biome color, darkened 15% on LargeHills and 30% on Mountainous. Rivers and roads drawn as 2-px lines between projected neighbor centers, from `SurfaceTile.Rivers` and `Roads`. Water is #2a4a63.
    - **Holo:** background #061218, a 40-px grid at #1d4a5a, the coastline as cyan #3fd0f0 where a land pixel touches water, elevation contours every 250 m (pixels where `floor(elev/250)` differs from a neighbor's) at #2aa3c0 with 55% alpha, and the biome as a 10% tint.
  - `BiomeColors.For(BiomeDef)`: the tuning table, or a fallback color hashed from the defName.

- [ ] **Step 1:** Implement it.
- [ ] **Step 2:** Add a dev action "Expeditions: dump map bake" that writes the texture to PNG in the save folder for both styles, and logs the bake time.
- [ ] **Step 3:** Check: both PNGs show coastlines, mountains and rivers in the right places compared with the vanilla world map, and the map is not mirrored (if it is, negate x in `ToPixel`). The bake takes under 300 ms at range 60.
- [ ] **Step 4:** Commit: `Add flat map bake`.

### Task 12: The Expeditions window, the look and the report

**Files:** Create `Source/UI/ExpeditionsLook.cs`, `Window_Expeditions.cs` (with partials `.Opportunities.cs`, `.Prospect.cs`, `.Active.cs`, `.Captured.cs`, `.Map.cs`), `Window_ExpeditionReport.cs`, and `Source/UIRethemedCompat/Expeditions.UIRethemed.csproj`, `LfsExpeditionsLook.cs`. Modify `Languages/English/Keyed/Expeditions.xml`.

**Interfaces:**
- Consumes: everything above.
- Produces:
  - `class ExpeditionsLook` (vanilla look) with `static Current`, `Use(ExpeditionsLook)` and `Blame(Exception)` as in CommRecruiter's `HireLook`. Primitives: `Frame(Rect)`, `Tab(Rect, string, bool active) → bool`, `Row(Rect, bool selected) → bool`, `Bar(Rect, float score01, float requirement01)`, `Button(Rect, string, bool enabled, string? disabledReason) → bool`.
  - `LfsExpeditionsLook : ExpeditionsLook` draws the same primitives with UI Rethemed widgets and builds to `Compat/UIRethemed/1.7/Assemblies/`, following CommRecruiter's `Compat/UIRethemed` project.
  - `Window_Expeditions(Thing console)`, sized 1100×720 and draggable, in layout A:
    - Title bar with the tabs Opportunities, Prospect, Active and Captured.
    - Left list 200 px wide.
    - Map in the remaining area, with a 190-px drawer under it.
    - Map: draw the baked texture with zoom (scroll, 1-4×) and pan (right drag). Markers drawn live as code-drawn diamonds in the site's color (holo: plus a larger translucent glow): home, faction settlements in range (small, grey), opportunities, expedition paths (dashed, home to PoI) with the squad's position interpolated by phase progress, captives' rescue camps, and the range ring. Clicking a marker selects it. A style toggle in the corner writes `Settings.style`.
    - Opportunities drawer: name, place, nominal days, known faction; one `Bar` per challenge with a tooltip listing each `Contribution.explanation`; squad slots (portraits; "+" opens a float menu of eligible pawns with each one's score); ride buttons from `RideFinder.For` (disabled with their reason); Success, Find and Away (days from `TravelMath.TripDays`); food needed against available; and Launch, enabled only by `Departure.CanLaunch`.
    - Prospect: a search box, rows with the item icon, label and chance, the "asked this window" state, and the time until the seed changes. Start opens a float menu of colonists who can reach the console and gives the prospect job.
    - Active: rows with phase, progress and portraits; selecting one highlights its path. History below: rows that open the report.
    - Captured: rows with captive, captor and days held, plus Rescue, which creates and selects the rescue opportunity.
  - `Window_ExpeditionReport(ExpeditionReport)`: an outcome banner, one row per pawn (portrait, fate, injuries, XP), loot, and lost-to-haul.
  - All strings keyed `Exp_*`.

- [ ] **Step 1:** Implement `ExpeditionsLook`, the window shell and the tabs.
- [ ] **Step 2:** Implement the map drawing and the drawer.
- [ ] **Step 3:** Implement the report window and the UI Rethemed compat assembly. Build both.
- [ ] **Step 4:** Check in game, compared against the mockups:
  - Opening twice: the second open is instant.
  - Tooltips name the gear and bionics.
  - A squad that's short on food has Launch disabled with the reason.
  - Toggling the style switches the map and is remembered after a restart.
  - With UI Rethemed active, the window uses its theme.
  - A full loop: launch on foot, report, rescue.
- [ ] **Step 5:** Commit: `Add Expeditions window and report`.

### Task 13: Settings, debug menu, About and the release check

**Files:** Modify `Source/Core/ExpeditionsMod.cs` (settings window), `About/About.xml` (final description). Create `Source/Debug/ExpeditionsDebug.cs` (collect the dev actions from Tasks 5-11 under the "Expeditions" category).

- [ ] **Step 1:** Write the settings window: map style, range (20-120), and ready-made offer count (1-10).
- [ ] **Step 2:** Move all dev actions into `ExpeditionsDebug` under `[DebugAction("Expeditions", ...)]`.
- [ ] **Step 3:** Write the final About description: what it does, the rides, prospecting, optional Vehicle Framework and UI Rethemed.
- [ ] **Step 4:** Release check:
  - `dotnet test Source/Tests` passes.
  - `dotnet build -c Release` passes for both projects.
  - A new colony with only Harmony and this mod: no errors in the log.
  - The same with UI Rethemed and with Vehicle Framework.
  - Go through review focus 1-5 again.
- [ ] **Step 5:** Commit: `Expeditions 1.0 settings, debug and About`.
