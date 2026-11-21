# Expeditions: Ideology work sites

Ideology's work sites (farming, logging, mining, hunting) show up on the expedition board. The player can still raid one by hand and take the whole stash, or send a squad and bring home at most 60% of it. This extends `expeditions-mod-design.md` and changes one of its non-goals (see *Non-goals*).

## Intent
A work site with 2000 berries is worth a raid, but a raid means forming a caravan, walking there and fighting on a new map. An expedition skips that for a cut of the loot: 1200 berries at best. The 40% you give up is the price of not going yourself. The squad still has to deal with the guards under the normal rules.

## Goals
- Every work site within range appears on the board as its own offer, tied to that site.
- The squad goes through the usual challenges, outcome roll and risk. Success brings back 60% of the stash, Partial half of that (30%), and Failure nothing. After that, the usual carry limit applies.
- Difficulty follows the site's guards. A big guarded camp is harder than a small one.
- A squad hit counts as a raid. The site goes away, the owner turns hostile as vanilla does it, and on Success or Partial it counts as raiding for Ideology precepts.

## Non-goals
- Placing anything on the world map. The base design's non-goal is narrowed to that: the mod now *reads* vanilla sites and removes them once raided, but still creates nothing there.
- A button on the work site itself (world map gizmo). This could be added later on top of this design.
- Other vanilla sites (bandit camps, item stashes, ancient complexes).
- Patching vanilla's work site quest or its spawn rate.

## Anti-goals
- Taking loot from a neutral or allied faction with no diplomatic cost.
- Two squads, or a squad and the player, both getting the same stash.
- Work site offers pushing the mod's own offers off the board.

## Approaches
1. **Watch the world map (chosen).** `OfferGenerator.Tick` also scans for work sites in range and makes an offer for each new one. It works on existing saves and picks up other mods' work sites. Nothing in vanilla is patched.
2. **Hook the site's creation.** A Harmony patch on `QuestNode_Root_WorkSite`. The offer shows up instantly, but sites already in a save are missed, and the patch breaks if the quest changes.
3. **Gizmo on the world map site.** Opens the mod window on that site. It sends the player to the world map, which the board exists to avoid.

The user picked 1.

## What vanilla gives us
Checked in the decompiled `SitePartWorker_WorkSite` and `QuestNode_Root_WorkSite`:
- A work site is a `Site` whose `MainSitePartDef.tags` contains `WorkSite` and whose part worker is a `SitePartWorker_WorkSite`.
- `site.parts[0].things` holds the real stash. `lootThings` lists the same items as def/count pairs.
- `site.parts[0].parms.threatPoints` and `site.parts[0].expectedEnemyCount` give the guard strength.
- The owner is a real faction (enemy, neutral or ally) or a temporary faction, which is created hostile to the player.
- The quest (`OpportunitySite_WorkSite`) times the site out after 30 days. It ends as Success when the site's threats are cleared, which fires the `Raided_WorkSite` history event (`successHistoryEvent`), and as Fail when the map is removed.
- Attacking a site in person sours relations through `SettlementUtility.AffectRelationsOnAttacked(site, ref letterText)` (called from `CaravanArrivalAction_VisitSite`).

## Design

### The site def
One `ExpeditionSiteDef`, `Exp_WorkSite`, shared by all four kinds. It is `MayRequire="Ludeon.RimWorld.Ideology"`, excluded from random offers the way `rescueOnly` sites are, and is never prospected.
- Challenges: Fight (weight 2, requirement from the guards, see below), Stealth (1.5, 100), Haul (1, 150), as at the hostile outpost.
- Risk: harsh, as at the hostile outpost. Time on site 0.5 days, squad 1-6.
- No `loot` entries, and `findChanceBase` 1 with no Search challenge. The stash is known, so it is always "found" unless the outcome is Failure.

The four kinds don't get their own challenge mixes. Heavy stashes (logs, steel) are already harder to bring home because of the carry limit.

### Fight scales with the guards
Fight is a Sum challenge, so 100 is one decent fighter. The requirement is `worksiteFightPerGuard × expectedEnemyCount`, with a minimum of 100. `worksiteFightPerGuard` is a new value in `Expeditions_Tuning.xml`, starting at 90: guards are workers, a bit below a decent fighter. The requirement is read from the linked site at evaluation time, so the offer panel and the resolution agree.

### The offer
- `Opportunity.worksite` (a `Site`, saved by reference) links the offer to its site. `faction` is the site's faction.
- The label is the vanilla site label plus the owner: "farming work site (Ashwood Union)".
- The offer panel shows the guard count and the best-case haul (60% of each stash item, e.g. "berries ×1200 at best").
- Work site offers don't count toward `offerSlots` (`ReadyMadeCount` skips them) and have no expiry tick of their own (`expiresTick = -1`).
- `OfferGenerator.Tick` adds an offer for each work site that is on a tile in range (`WorldArea.InRange`), has no map (nobody is raiding it), has a non-empty stash, and isn't already linked to an offer or an active expedition. It runs every tick pass, not only when `nextOfferTick` comes round, so a new site shows up within a few seconds.
- `StillFits` withdraws the offer when its site is destroyed or has a map.

### Loot
In `LootRoller.Roll`, an offer with a work site rolls no loot table. For each item def in the stash: `count = floor(total × worksiteShare)`, then the usual Partial factor and `Scale`. `worksiteShare` is a new tuning value, 0.6. New things are made by def, so vanilla's stash is left alone until the site is removed. Then `Fit` cuts the result to what the squad can carry, as for every site, and the report lists what was left behind.

### Consequences
At resolution, whatever the outcome, if the site still exists:
1. **Relations:** `SettlementUtility.AffectRelationsOnAttacked(site, ref text)`, as when attacking in person. A temporary faction is already hostile, so nothing changes for it. Any letter text it produces goes into the report or a message, not a second letter.
2. **The quest:** find the site's quest and end it, Success on Success or Partial (this fires `Raided_WorkSite` for raider precepts) and Fail on Failure.
3. **The site:** destroy it if the quest end didn't.

The resolution runs under the expedition's `Rand` seed as now. The consequences above don't use `Rand`.

### The site is gone before the squad gets there
New behavior. While an expedition is Outbound or OnSite, `Tick250` checks its work site. If the site is destroyed (timed out) or has a map (the player is raiding it), the squad turns back:
- It starts Returning with the time it has spent out so far, so it comes home the way it went.
- Nothing is resolved: no challenges, no fates, no XP, no consequences.
- The report has a new outcome, Gone: "The work site was gone by the time they got there." It goes into history like any other report.

Food: the squad carried enough for the full trip, so turning back early never runs short.

## Left to the builder
- How to find the site's quest: for example the quest whose `QuestPart_WorldObjectTimeout` (or look targets) points at the site.
- Where the guard-scaled requirement plugs in: `SquadEvaluator` reads `SiteChallenge.requirement`. It needs an override for the work site's Fight entry without changing the shared def.
- Icon: one icon for `Exp_WorkSite`, or the vanilla site's `expandingIconTexture` per kind on the map marker. Either works.
- New report fields for the "site gone" case, and the English keys.
- `Raided_WorkSite` and the relations change use vanilla's own calls. Check in game that the raider precept fires once, and that a neutral owner turns hostile with vanilla's letter.

## Testing
- Logic: the stash share math (`floor`, Partial, minimum 1) goes into `Source/Logic` with a test, next to `LootFit`.
- In game, with Ideology: use the dev quest tool to generate `OpportunitySite_WorkSite`, then check:
  - the offer appears with the right label, guards and best-case haul
  - Success brings 60%, Partial 30%, and a small squad on foot leaves some behind
  - the site disappears, the quest ends, and a neutral owner turns hostile
  - letting the site time out mid-trip sends the squad home with a "gone" report
  - raiding it by caravan mid-trip does the same
  - without Ideology, nothing changes and no errors appear
