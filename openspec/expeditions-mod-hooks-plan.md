# Mod hooks and BOR sites: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expeditions gains generic hooks and four new sites. BOR gains a `Mods/Expeditions` folder with nine sites and its data for the generic ones.

**Architecture:** Expeditions adds XML-driven fields and small abstract classes: `SiteGate`, `PawnEffect`, `OutcomeEffect`, `ExpeditionLootFilter`, `ExpeditionFactionDef` and `SettlementLinkWorker`, wired into the offer, resolve and arrival code. BOR ships defs, patches and a compat assembly, `BORExpeditions.dll`, which references both `BeyondOurReach.dll` and `Expeditions.dll` and loads only when Expeditions is active.

**Tech Stack:** C# (net10.0 against RimWorld 1.7's Assembly-CSharp), RimWorld XML defs and patches, xunit tests over `Source/Logic`.

**Spec:** `openspec/expeditions-mod-hooks-design.md` (Expeditions) and `../BeyondOurReach/openspec/expeditions-sites-design.md` (BOR).

## Global Constraints
- Expeditions never names a BOR def or references BOR's assembly.
- BOR's main assembly never references Expeditions. Only `Mods/Expeditions/Source` does.
- Pure math goes in `Source/Logic`, with no Verse, RimWorld or UnityEngine, and has tests in `Source/Tests`.
- Every hook call into another mod's worker is wrapped in try/catch and logs once per def. A bad worker never loses a squad.
- Old saves load. New saved fields have defaults.
- Commit messages carry no AI attribution.
- User-visible strings go in `Languages/English/Keyed`: `Exp_` keys in Expeditions, `BOR_` keys in BOR.

## Review Focus
- A gated site whose gate closes while it is on the board is withdrawn, and a squad already out is untouched, except a settlement worker's `StillWanted`.
- A settlement link offer disappears when its settlement is destroyed or changes hands, and an away squad turns back.
- Faction payment with a silverReward smaller than one unit of the goods pays silver, not nothing.
- `untilAnalyzed` on a thing with no analysable comp is a config error, not a silent always-drop.
- A pawn effect that throws doesn't stop the other pawns' effects or the resolve.

---

### Task 1: Logic helpers and tests
**Files:** Create `Source/Logic/HookMath.cs`. Test: `Source/Tests/HookMathTests.cs`.
**Produces:**
- `HookMath.SplitPayment(double silver, IReadOnlyList<double> unitValues) -> (int[] counts, int silverLeft)`: the value is split evenly across the kinds, each count floored, and the rest paid as silver.
- `HookMath.JoinerChance(double chance, Outcome o, double partialFactor) -> double`: Success gives `chance`, Partial gives `chance*partialFactor`, otherwise 0.
- `HookMath.SettlementFight(double points, double perPoint, double min) -> double`: `max(min, points*perPoint)`.
- `HookMath.HalfUp(int n) -> int`.
- `HookMath.CappedKills(int have, int add, int killsToPod, double podThreshold) -> int`: the most kills that can be added while `podThreshold*(have+added)/killsToPod < 1`.
- [ ] Tests:
  - `SplitPayment(300, [10, 20])` gives counts `[15, 7]` and silver `10`.
  - `SplitPayment(5, [10])` gives `[0]` and `5`.
  - `JoinerChance(0.6, Partial, 0.5)` gives `0.3`.
  - `CappedKills(0, 10, 100, 0.5)` gives `10`.
  - `CappedKills(190, 50, 100, 0.5)` gives `9`.
  - `CappedKills(199, 5, 100, 0.5)` gives `0`.
- [ ] `dotnet test Source/Tests` passes, then commit.

### Task 2: Gates and immediate offers
**Files:** Create `Source/Defs/SiteGate.cs`. Modify `Source/Defs/ExpeditionSiteDef.cs`, `Source/World/OfferGenerator.cs`, `Source/Prospect/ProspectService.cs`, `Source/Debug/ExpeditionsDebug.cs`.
**Produces:**
- `abstract class SiteGate { abstract bool Open(); virtual string? ClosedReason; virtual int? DeadlineTick; virtual IEnumerable<string> ConfigErrors(); }` and `SiteGate_Research { List<ResearchProjectDef> research, anyResearch; }`.
- On `ExpeditionSiteDef`: `List<SiteGate> gates`, `bool immediate`, `bool GatesOpen`, `int? GateDeadline`.
- [ ] `ReadyMadeOrder` and `MakeLead` skip closed sites. `StillFits` withdraws an offer whose site has closed gates. Debug spawns ignore gates, so tuning stays possible.
- [ ] `OfferGenerator.Tick` handles immediate sites: for each immediate site whose gates are open and has no offer and no squad out, it makes one with `TryMake(site, home, null)`. Immediate sites are excluded from `ReadyMadeOrder` and from `ReadyMadeCount`.
- [ ] `TryMake` sets `expiresTick` to `GateDeadline` when there is one.
- [ ] Build, then commit.

### Task 3: Loot entry conditions
**Files:** Modify `Source/Defs/ExpeditionSiteDef.cs` (`LootEntry`) and `Source/Resolve/LootRoller.cs`. Create `Source/Resolve/Analysis.cs`.
**Produces:**
- `LootEntry.research` (`ResearchProjectDef?`) and `LootEntry.untilAnalyzed` (bool).
- `LootEntry.Live() -> bool`.
- `Analysis.Done(ThingDef) -> bool`, which reads `CompProperties_CompAnalyzableUnlockResearch.analysisID` and calls `Find.AnalysisManager.TryGetAnalysisProgress(id, out var d) && d.Satisfied`. That's the same call BOR's `SignalCore.Analyzed` uses.
- [ ] `AddTable` skips an entry where `!Live()`.
- [ ] Config error: `untilAnalyzed` on a thing with no analysable comp.
- [ ] Build, then commit.

### Task 4: Faction defs on sites, and faction overrides
**Files:** Modify `ExpeditionSiteDef.cs` and `OfferGenerator.cs`. Create `Source/Defs/ExpeditionFactionDef.cs`. Modify `LootRoller.cs` and `Window_Expeditions.Opportunities.cs`, where the payment shows in info lines.
**Produces:**
- `ExpeditionSiteDef.factionDefs` and `enemyFactionDefs` (`List<FactionDef>`). `OfferGenerator.Wants(FactionNeed, Faction?, List<FactionDef>?)` is overloaded.
- `ExpeditionFactionDef { FactionDef faction; List<SiteGate> gates; List<ThingDef> payment; List<FactionSiteLoot> siteLoot; List<FactionSiteEffects> outcomeEffects; static ExpeditionFactionDef? For(Faction?) }`. The lookup is live only when its gates are open.
- `FactionSiteLoot { ExpeditionSiteDef site; bool replace; List<LootEntry> loot; }` and `FactionSiteEffects { ExpeditionSiteDef site; List<OutcomeEffect> effects; }`.
- [ ] `LootRoller.Roll`:
  - With a live patron override that has `payment`, silver goes through `HookMath.SplitPayment`, using `BaseMarketValue` as the unit value.
  - With a live holder override for this site, its loot is added to the site's table or replaces it.
- [ ] Build, then commit.

### Task 5: Pawn and outcome effects, and the goodwill effect
**Files:** Create `Source/Defs/Effects.cs`. Modify `ExpeditionSiteDef.cs`, `Resolver.cs`, `Arrival.cs` and `Expedition.cs` (which saves the outcome for arrival). Add a report field in `ExpeditionReport.cs`.
**Produces:**
- `abstract class PawnEffect { List<Outcome> outcomes; abstract void Apply(Pawn, Expedition, Outcome, ExpeditionReport); }`
- `abstract class OutcomeEffect { List<Outcome> outcomes (default Success); virtual void Apply(Expedition, Outcome, ExpeditionReport); virtual void OnArrival(Expedition, Outcome, Map?, ExpeditionReport); }`
- `OutcomeEffect_GoodwillAll { int amount; bool includeHostile; List<FactionDef> exceptFactionDefs; }`, halved on Partial.
- `ExpeditionReport.effectLines` (`List<string>`, saved), printed after the goodwill lines in `ReportText`.
- `Effects.Safe(Def owner, Action a)`: try/catch that logs once per owner.
- [ ] Resolver:
  - Pawn effects run after fates, on humanlikes whose fate is Unharmed or Injured.
  - Outcome effects (site and faction override) run with `Apply` after `Rand.PopState`.
  - The outcome is stored on the expedition. It is already in `report.outcome`; use that.
- [ ] Arrival runs `OnArrival` with the map, or null for a caravan, before writing the letter.
- [ ] Build, then commit.

### Task 6: Loot filters
**Files:** Create `Source/Resolve/LootFilters.cs`. Modify `LootRoller.cs` and `Arrival.cs`.
**Produces:**
- `abstract class ExpeditionLootFilter { abstract string? Filter(Expedition e, List<Thing> loot); }`.
- `LootFilters.All` holds one instance per subclass, found with `GenTypes.AllSubclassesNonAbstract`.
- `LootFilters.Run(Expedition, List<Thing>, ExpeditionReport)` destroys the struck things and adds their lines to `effectLines`.
- [ ] It runs before `Fit` in `Roll`, and in `Arrival.Return` on the non-pawn contents before they are released.
- [ ] Build, then commit.

### Task 7: Joiners on Partial, and mech joiners
**Files:** Modify `ExpeditionSiteDef.cs` (`JoinerSpec`) and `Resolver.cs` (`Recruits`). Modify `Window_Expeditions.Opportunities.cs` for the mechanitor note.
**Produces:**
- `JoinerSpec.partialFactor` (float).
- `JoinerSpec.mechKinds` (`List<MechKindEntry>`), where `MechKindEntry { PawnKindDef kind; ResearchProjectDef? research; }`.
- [ ] Joiners roll on a found Success, or on a found Partial when `partialFactor > 0`, at `HookMath.JoinerChance`.
- [ ] Mech joiners:
  - They need a squad pawn with a mechlink (`MechanitorUtility.IsMechanitor`), and the kind is picked from the entries whose research is finished.
  - They are generated with `Faction.OfPlayer`, and the overseer is set with `pawn.GetOverseer()`. The builder uses `mechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech)`.
  - With no mechanitor, the roll is skipped and the report says so.
- [ ] Config error: joiners need `kind` or `mechKinds`.
- [ ] Build, then commit.

### Task 8: Settlement links
**Files:** Create `Source/World/SettlementLinkProps.cs` and `Source/World/SettlementLinks.cs`. Modify `Opportunity.cs` (`linkedSettlement`, saved as `"settlement"`), `OfferGenerator.cs`, `Resolver.cs`, `Retaliation.cs`, `ExpeditionsGame.cs` (turn back), `ExpeditionSiteDef.cs` and the opportunities UI (info lines).
**Produces:**
- `enum SettlementEnd { Raid, Destroy, Nothing }`.
- `SettlementLinkProps { Type workerClass; FactionNeed need; List<FactionDef> factionDefs; SettlementEnd onSuccess, onPartial; float fightPerPoint=1, fightMin=200; bool raidCredit, affectRelations; RetaliationProps? retaliation; }`.
- `class SettlementLinkWorker`:
  - `def`
  - `virtual bool CanOffer(Settlement)`
  - `virtual bool StillWanted(Opportunity)`
  - `virtual float FightRequirement(Settlement)`
  - `virtual IEnumerable<TaggedString> InfoLines(Opportunity)`
  - `virtual void Conclude(Settlement, Outcome, ExpeditionReport, List<Pawn> raiders)`
- `SettlementLinks.Alive(Opportunity) -> bool`, `SettlementLinks.Lost(Expedition) -> bool`, and `SettlementLinks.Destroy(Settlement)`. `Destroy` follows vanilla's `SettlementDefeatUtility`: `DestroyedSettlement`, remove the settlement, mark the faction defeated with its letter when it has no settlements left.
- [ ] Offering: ready-made through `TryMake`.
  - The tile is a qualifying settlement in range.
  - It skips settlements already linked to an offer or a squad.
  - With `needsFaction`, the patron is found near the settlement and the owner goes to `enemy`. Otherwise the owner goes to `faction`.
- [ ] `Opportunity.Requirement` uses the worker's fight requirement for `Exp_Fight` when `linkedSettlement` is set.
- [ ] Tick: `SiteLinks.Lost || SettlementLinks.Lost` → turn back. `TurnBack` is generalized to clear `linkedSettlement` too.
- [ ] Resolver: after the effects, if `linkedSettlement` is alive, `Conclude` runs:
  - relations on any rolled outcome;
  - raid credit on Success and Partial;
  - the end by outcome.
  - `Retaliation.For` reads `settlementLink.retaliation`, with the settlement's owner as the raider.
- [ ] Report: `siteFateText` says "razed" or "raided".
- [ ] Build, then commit.

### Task 9: Four generic sites, keys and player site
**Files:** Create `Defs/Expeditions_Sites_More.xml`. Modify `Languages/English/Keyed/Expeditions.xml`, and `site/build.py` (labels and defaults for new fields).
- [ ] `Exp_DownedCourier`, `Exp_SlaverConvoy`, `Exp_SettlementBounty` and `Exp_DormantMechs` (`MayRequire` Biotech), with the numbers from the hooks doc, *New sites*.
  - Courier rare item: `successOnly`, chance 0.4, category `Neurotrainers` with Royalty, else `Techprints`; or plain `ComponentSpacer` ×2 if neither loads. The builder checks the def names.
- [ ] `python site/build.py --strict` passes, or the problems are fixed.
- [ ] Build, test, commit.

### Task 10: BOR compat project and gates
**Repo:** BeyondOurReach, on a new branch `expeditions-sites`.
**Files:**
- Create `Mods/Expeditions/Source/BORExpeditions.csproj`, which references Assembly-CSharp, UnityStubs, `..\..\..\Assemblies\BeyondOurReach.dll` and `..\..\..\..\Expeditions\1.7\Assemblies\Expeditions.dll`, all with `Private` False. Output goes to `..\Assemblies\`.
- Create `Mods/Expeditions/Source/Gates.cs`.
- Modify `LoadFolders.xml`.
**Produces:**
- `BeyondOurReach.SiteGate_Path { ColonyPath path; bool not; }`.
- `BeyondOurReach.SiteGate_PactObjective { List<DarkPactObjective> objectives; }`. Its deadline is the pact's end.
- [ ] Build, then commit (BOR).

### Task 11: BOR effects, filter, touch hediff, BOR main-assembly entry points
**Files:**
- Create `Mods/Expeditions/Source/Effects.cs` and `Mods/Expeditions/Source/LootFilter.cs`.
- Create `Mods/Expeditions/Defs/Hediffs_Touch.xml`. It must be loadable without Expeditions, so it goes in `Common/Defs/HediffDefs/Hediffs_Orientis.xml`, next to `BOR_Blessing`.
- Modify `Source/Orientis/Blessing.cs`: a touched part is unavailable.
- Modify `Source/Tenebral/DarkPactQuest.cs`: public `AddProgress(int)`.
**Produces:**
- `PawnEffect_TenebriteExposure { float emission = 3; }`
- `PawnEffect_TouchOfTheHoly { float chance; }`
- `PawnEffect_Reaping { IntRange kills; }`: capped with the logic of `HookMath.CappedKills`, written inline in BOR since BOR can't use Expeditions' `Logic` tests. It adds the total to a `KillPeople` pact.
- `OutcomeEffect_MeditationPower { float amount; }` (on arrival).
- `OutcomeEffect_Scrip { FloatRange amount; List<ScripPerThing> perThing; }` (on arrival).
- `OutcomeEffect_PactProgress`.
- `BORLootFilter`.
- `QuestPart_DarkPact.AddProgress(int n)` for counted objectives: it adds to progress and calls `Succeed()` once the goal is met.
- [ ] Build both BOR assemblies, then commit (BOR).

### Task 12: BOR site defs, faction overrides, patches, keys
**Files:**
- Create `Mods/Expeditions/Defs/Sites.xml`, `Mods/Expeditions/Defs/Factions.xml` and `Mods/Expeditions/Patches/GenericSites.xml`.
- Create `Languages/English/Keyed/BOR_Expeditions.xml`.
- [ ] The nine sites from the BOR doc, *Sites*, with their gates, effects, numbers and commented-out Hunger effect.
- [ ] The overrides and patches from *Data for generic sites*. Badzaar guns are listed by their `BOR_Gun_Badzaar_*` def names.
- [ ] Build, then commit (BOR).

### Task 13: Whole-branch review, in-game start check
- [ ] A fresh reviewer (Fable) checks both branches against both docs. Fix the findings.
- [ ] Start RimWorld with Expeditions and BOR, and check `Player.log` for errors from either mod. Fix them and repeat.
