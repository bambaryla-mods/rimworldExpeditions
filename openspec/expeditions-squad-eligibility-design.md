# Expeditions: who can go

Today the squad picker offers every free colonist on the map (`FreeColonistsSpawned`) plus pack animals. That list includes quest guests, other mods' temporary pawns, slaves, ghouls and children, and nothing checks them. This doc sets the rules for who may join a squad, and what happens when a temporary or untrustworthy pawn is already out on a trip.

## Intent
Two problems, both real:
- **Breaking other systems.** Away pawns sit frozen in the expedition holder. Quests and mods that own a pawn keep running on the game clock and may pull the pawn out of our holder, or leave it stranded past its time. CommRecruiter does exactly this: when a contract ends, `ReturnOffscreen` removes the hiree from whatever holds it and sends it to the world.
- **Too generous.** Borrowed or hosted pawns should not be free expedition labour.

The rule of thumb: a pawn goes only if Expeditions can keep its status correct for the whole trip. Expeditions owns this. Other mods do not need to know Expeditions exists.

## Goals
- Royalty guests, their escorts and every other quest lodger are refused, unless Expeditions knows when they leave and the trip fits.
- CommRecruiter hirees can go when their contract outlasts the trip.
- A hiree recalled early (home faction turned hostile) ends the trip early.
- Slaves need an overlord in the squad and may run away.
- Ghouls (any mutant) are refused.
- Creepjoiners can go, without the picker or the odds giving them away. A Leaves or Aggressive downside that comes due mid-trip happens on the trip.
- Children can go, at a cost.

## Non-goals
- Detecting temporary pawns that carry no vanilla marker (a summon with a private timer, say). We cannot see them; they are treated as ordinary colonists. Support for a specific mod is added case by case, the way CommRecruiter is.
- Changing CommRecruiter.
- Changing who can prospect. Prospecting is an on-map job and keeps its own list.
- Re-checking squads already away when this ships. The new launch rules apply to new launches; the mid-trip events (recall, creepjoiner) apply to every trip.

## Anti-goals
- A temporary pawn kept past the time it is due to leave.
- A pawn vanishing from a squad with no report line.
- The picker, the odds or a refusal reason revealing that someone is a creepjoiner.
- A way to cancel a bad roll for free (getting a hiree recalled to abort an expedition and get the offer back).
- A squad-wide bonus for bringing a child. A child is a liability.

## Approaches
- **A. Expeditions reads CommRecruiter itself, by reflection.** No change to CommRecruiter, no dependency either way. Ties Expeditions to CommRecruiter's internals; every further mod is another reader in Expeditions.
- **B. Expeditions exposes a hook that mods register with.** Cleaner for third parties, but CommRecruiter would have to change and know about expeditions.
- **C. Both: the hook, plus a built-in CommRecruiter adapter.** Most code.

**Picked: A.** The user wants this to be Expeditions' problem: CommRecruiter should not carry rules about expeditions. The coupling cost is accepted. The reader lives in its own small file so it can be found and fixed when CommRecruiter changes.

## Rules at pick and launch
One place decides whether a pawn may go and why not (call it `Eligibility`). The picker and `Departure.CanLaunch` both use it, so a pawn that became ineligible after being picked (a contract that ran down while the window was open) is caught at launch.

### Per pawn
| Pawn | Rule | Reason shown |
| --- | --- | --- |
| Mutant (ghoul and any other) | refused. Ghouls are already absent from `FreeColonistsSpawned` (`IsColonist` is false for subhuman mutants) and stay unlisted; the rule catches any non-subhuman mutant that is listed. (My call.) | "Can't go on expeditions." |
| Baby | refused (can't walk). Today they are likely not in the list at all; keep it that way. | none needed |
| CommRecruiter hiree, not retained | allowed only if the contract ends after the trip's planned return plus a margin | "Contract ends in X, trip takes Y." |
| CommRecruiter hiree, retained | ordinary colonist | — |
| Any other quest lodger (`IsQuestLodger`) | refused | "Guest of a quest: can't leave." |
| Slave | allowed; squad rule below | — |
| Creepjoiner | ordinary colonist, no mark of any kind | — |
| Child | allowed; penalties below | — |

A hiree counts as a quest lodger in vanilla (its hidden contract quest gives it an extra home faction), so the hiree check runs first.

Contract fit: `contract end ≥ launch tick + planned trip length + margin`. The trip length is the one `Departure.TripDays` already computes. The margin covers the Departing phase (walking to the edge, boarding) and is a tuning value, default 1 day.

### Squad
- **Overlord:** a squad with any slave needs at least one free, non-slave, adult colonist of the player's own. A non-retained hiree does not count (the user's call). Reason: "Slaves need a free colonist to keep them in line."
- Existing size, food, ride and console checks are unchanged. Slaves and children count as people for squad size.

### Picker presentation
Refused pawns stay in the "+" menu, greyed out, with the reason as the disabled text, so the player knows why. Babies are not listed. This is my call: hiding refused pawns would confuse more than it helps, and a reason on a ghoul or a guest gives nothing away.

## Children
A child is a pawn whose developmental stage is Child (Biotech).
- Each challenge def gets a `childFactor` (default 1). A child's own score on that challenge is multiplied by it, before the squad's scores are combined.
- Starting values: `Exp_Fight` 0.5, `Exp_Contest_Shooting` 0.5, `Exp_Contest_Melee` 0.5, `Exp_Haul` 0.5. Everything else 1.
- This stacks on top of the vanilla penalties a child already has (low skills, small body). That is intended.
- No stealth bonus, personal or squad-wide. A child rolls Stealth like anyone else, and since Stealth takes the squad's minimum, a clumsy child can be the weak link.
- Vehicle cargo added to Haul is not scaled.
- The breakdown tooltip names the child factor so the player sees why the number dropped.

## Slaves
- Suppression is read and stored on the expedition at launch (it freezes while away anyway).
- **Runaway roll:** each slave rolls once when the expedition resolves. The chance falls as suppression rises: a tuning curve, starting at 40% at 0 suppression, 15% at 50%, 3% at 100%. The roll is seeded like the other outcome rolls.
- **No overlord left:** whenever the squad is left with no overlord (as defined at launch) alive, not captured and still in the squad, every surviving slave runs, no roll. This is checked after resolution, after a creepjoiner fight, and after a creepjoiner walks off, in any phase.
- A slave who runs never comes home. They still count for that trip's challenges, since they were there. They are not a captive. Report fate: a new `PawnFate.RanAway`.
- What becomes of the pawn: released from slavery and from the player faction and passed to the world, following vanilla's own slave escape where it fits. The builder checks which vanilla call does this cleanly without a map.
- An early-ended trip (below) skips resolution, so no runaway roll happens on it. The no-overlord rule still applies.

## Early end
Two things end a trip early: a hiree recalled, and an aggressive creepjoiner turning.
- There is no resolution: no challenge roll, no loot, no find, no XP, no retaliation, no slave runaway roll.
- The squad heads straight home with the existing turn-back timing (`SiteLinks.TurnBack`): as long as they have travelled if still outbound, the full way back if on site. Food is cut the same way.
- The offer does not go back on the board; it left the board at launch. A linked vanilla site still exists on the world map and the offer generator may offer it again like any other site in range. That is fine: it is the site, not a refund.
- Report outcome: a new `Outcome.CutShort` with a reason line (who was recalled, or what the creepjoiner did). Wording must not spoil anything that was not already shown by the event itself.
- During Departing (still on the map) nothing new is needed: a recalled hiree walks off and the existing "squad fell apart" cancel handles it.
- During Returning: a pawn pulled out is noted in the report and the squad keeps walking. No other change.

### Recalled hiree
- CommRecruiter removes the pawn from our holder itself. Expeditions notices in its 250-tick loop that a squad member is no longer in `contents` and is not dead in it, and ends the trip early.
- The squad list must forget the pawn so Arrival, the colonist bar and the captive tracker skip it.
- CommRecruiter handles its own goodwill and letters. Nothing on our side.
- A recall mid-trip means the hiree's faction turned hostile (the contract check rules out a contract ending on the way). Hostile hirees do not leave empty-handed (the user's call):
  - **Only hirees and pack animals left:** they take everything: animals, ride, cargo, food. The expedition is lost, through the existing "nobody left" path in `Arrival`, with its own letter and report line saying the hirees made off with it.
  - **Hirees and the player's own pawns:** the hirees try to slip away with loot first, and only fight if they are caught (the user's call).
    - **Slip away:** a roll weighted by the leaving hirees' own Stealth score (the Stealth challenge's stats, `Min` across the hirees, as a squad's Stealth is scored) against a tuning requirement (starting 100, a decent sneak), through the existing ratio curve. Seeded like other rolls.
    - **Success:** they leave unseen with what they can carry: food and cargo up to their combined carrying capacity, taken from the expedition's holder (my call; animals and the ride are too noticeable to take quietly). Nobody is hurt. The report says they left in the night and lists what went with them.
    - **Caught:** a fight, using the fight rules of the aggressive creepjoiner, with the hirees' combined Fight score as the enemy. If the squad wins, the hirees are killed or flee (seeded per hiree) and nothing is lost. If the hirees win, they take the pack animals, the ride and the cargo, and the survivors walk home: the return time is recomputed on foot.
    - Either way the trip ends early.
  - The hirees themselves are already gone from the holder when this runs (CommRecruiter pulled them). Their Stealth and Fight scores are taken from the pawns as they are at that moment; a hiree "killed" in the fight is killed as a world pawn.
- Slaves: hirees never count as overlords, so a recall alone never leaves slaves unsupervised. If only slaves and hirees were left, the slaves were never allowed to launch.

### Creepjoiner downsides
Vanilla checks a creepjoiner's downside every 15000 ticks, only while the pawn ticks and is on a map. In our holder it never fires; a timer that ran out during the trip would fire within hours of coming home. We take over two of them on the trip:
- **Leaves** and **Aggressive**, when their trigger tick (`downsideTriggersAt`, private) falls before the squad would be home.
- Expeditions checks away creepjoiners in its loop. When the tick passes and the downside has not triggered, it fires the trip version and marks the downside triggered in the tracker (private, by reflection), so vanilla does not fire it again at home.
- Other downsides (crumbling mind, organ decay, psychic agony, disturbing, metalhorror, entity jailbreaker) are left to vanilla and happen after the squad is home.
- Nothing about this shows in the picker or the odds.

**Leaves:** the creepjoiner walks off. They leave the faction and pass to the world, as vanilla's depart worker does. The trip carries on; this is not an early end. Report fate: `RanAway`, with a line that they walked off.

**Aggressive:** an early end, plus a fight. The downside runs the pawn's own `aggressive` def, rolled separately, and all three vanilla kinds are handled on the trip (the user's call, so that what happens at home never differs from what happens away):
- **Assault:** the enemy is the creepjoiner, at their own Fight score times a tuning multiplier (starting 1.5, since a turned creepjoiner is dangerous).
- **FleshbeastEmergence:** the creepjoiner bursts into fleshbeasts. The enemy is a fixed fleshbeast pack strength (tuning value). The creepjoiner is gone for good: report fate `Killed`, with a line saying what happened.
- **SightstealerAttack:** the enemy is a sightstealer pack sized from the colony's threat points, the way vanilla sizes it. The creepjoiner is in the squad's way like any hostile and follows the Assault fate rules.
- A modded aggressive kind that is none of these falls back to Assault.

For every kind:
- Strength: the rest of the squad's Fight score (humanlikes only, child factor applied, no vehicle or armed bonus) against the enemy strength above.
- From the ratio, a tuning curve gives wound severity for each squad member, and a second curve a death chance per member. Starting points: at a ratio of 3 or more, light wounds and no deaths; at 1, heavy wounds and about 15% death each; at 0.5 or below, about 40% death each.
- Wounds use `InjuryMaker` (already tended). Deaths kill the pawn in the holder as resolution does today.
- The creepjoiner (Assault and Sightstealer): if the squad wins (ratio roll seeded like other rolls, favouring the stronger side), the creepjoiner is killed and left behind. Otherwise they escape and pass to the world as hostile. Either way they do not come home. Report fate: `Killed` or `RanAway`.
- Score conversion for fleshbeast and sightstealer strength (threat points into Fight score) follows the way linked sites already turn threat points into a Fight requirement.
- Pack animals take no part.
- **During Returning** (my call): the fight still happens with the same wounds and deaths, but there is nothing left to cut short. The resolved outcome, loot and XP stand, and the report gets the fight's lines added.
- Slaves are in the fight like everyone else. If no free colonist survives it, the slaves run (same rule as resolution).

## Tuning
New values in `Expeditions_Tuning.xml`: contract margin, slave runaway curve, aggressive creepjoiner multiplier, fleshbeast pack strength, hiree slip-away requirement, fight wound curve, fight death curve. `childFactor` lives on each challenge def.

## Saves
- New fields (stored suppression, new report fates and outcome) load with safe defaults on old saves.
- An old save with a guest or ghoul already away keeps that trip; the pawn just comes home.

## Testing
- Pure math (contract fit, runaway chance, fight outcome curves, child factor in scoring) goes in `Source/Logic` with unit tests, like the existing math.
- In game, with dev tools: a hiree whose contract is too short is refused; a hiree recalled mid-trip ends the trip; a slave squad without an overlord is refused; a creepjoiner with a forced Leaves or Aggressive downside timer fires mid-trip; a royal guest is refused; a ghoul is not offered; a child lowers Fight and Haul.

## Left to the builder
- Exact vanilla calls for slave release, creepjoiner departure, and hostile escape without a map.
- What a squad that lost its food to fleeing hirees suffers on the way home (arriving hungry is fine; nobody should starve to death off-screen without a report line).
- Reflection details for CommRecruiter (`GameComponent_CommRecruiter.RecordFor`, `HireeRecord.TicksLeft`, `retained`) and the creepjoiner tracker's private fields. Fail safe: if the reflection breaks, a hiree is treated as a refused quest lodger, and a creepjoiner as an ordinary colonist.
- Wording of reasons and report lines, in `Languages/English/Keyed/Expeditions.xml`.
- Whether the site docs (`site/content/`) get a "who can go" section.
