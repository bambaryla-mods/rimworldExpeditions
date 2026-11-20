---
summary: The window, the squad, and where the Success, Find and Away numbers come from.
card: Exp_AbandonedMine, Exp_CrashedShip, Exp_RiverCache
---
## The window

Select a powered comms console and press **Expeditions**. Opening it needs no pawn. The window has four tabs:

- **Opportunities**: what is on the board right now, in a list and on the map.
- **Prospect**: ask around for an item. See [Prospecting](prospecting.html).
- **Active**: squads that are out, what they are doing and how long it will take, and the reports of the ones that came back.
- **Captured**: colonists held by other factions. See [Captives](captives.html).

The map is your colony's surroundings, flattened around your home tile out to the expedition range. Pick an opportunity in the list or on the map to see its details.

Without a powered console, squads that are already out still come home, but you can't send new ones.

## Opportunities

Up to five opportunities are on the board at a time (you can change this in the mod settings). While there is room, a new one turns up every {{ tuning("offerIntervalTicks", "days") }} days, and each one goes away {{ tuning("offerLifetimeTicks", "days") }} days after it appears.

Each one is a kind of site that fits the land around you: a mine in the hills, a shipwreck on the coast, a hostile outpost when an enemy faction lives nearby, a job when an ally asks for help. The [sites](finds.html) pages list every kind and what it needs.

## The squad

Pick colonists who are on the console's map and free: not downed, not in a mental state, not already away. They go as they stand. What they wear and carry is what they have out there.

Each site sets how many colonists it takes. Pack animals can come along to carry more.

Then pick a ride. On foot is always there; drop pods, a shuttle or vehicles show up when you have them. The ride changes the travel time, how much comes home, and whether the squad can sneak in. See [Rides and food](rides.html).

## The numbers

Before you launch, the window shows three numbers:

- **Success**: the chance it goes to plan.
- **Find**: the chance there is anything to find. A squad can get in and out and still come back empty-handed.
- **Away**: how many days the squad will be gone, there and back, including the time on site.

### Success

Every site has a few [challenges](challenges.html), such as fight, stealth, haul or search. Each challenge reads some of your colonists' stats and skills. A decent pawn scores 100 on a challenge; a better one scores more, up to twice as much per stat, so one superstat can't carry it alone.

How the squad's scores make one number depends on the challenge. For a fight the scores add up, so more fighters help. For stealth the weakest pawn decides. For a search one expert is enough.

That squad score is then compared with what the site asks for:

{{ ratio_curve() }}

The chances of all the site's challenges are averaged by their weight to give Success, which never goes below 5% or above 95%. Hover a challenge bar in the window to see each pawn's part in it, and what raised or lowered it.

### Outcome

When the squad arrives, the dice are rolled against the Success you were shown:

- **Success**: everything went to plan. Full loot and full rewards.
- **Partial**: it went partly to plan. Half the loot, half the goodwill. {{ tuning("partialBand", "pct") }} of the rolls that miss Success come out Partial.
- **Failure**: nothing to take home, and the worst odds for the squad.

### Find

A site that has something to take has a chance that it is really there. Sites with a search challenge raise that chance with a good searcher, from half the base chance up to all of it. The sites pages show the range for each site.

Some loot only turns up on a full Success: the rare single items, such as the mechlink in the mechanitor complex.

### Risk

Each pawn rolls on the site's odds for the outcome: unharmed, injured, killed or captured. Injuries are real injuries, gear wears down, and a pawn can lose a body part. Deaths are reported at once by letter; everything else waits for the report.

Mechs, insects and beasts don't take prisoners, so at those sites capture is not one of the outcomes. Death is.

Everyone who comes back learns from it: the skills behind each challenge they took part in gain experience.

## The report

When the squad gets home, a letter opens the report: how it went, what they found, what they brought home and what they had to leave behind, and what happened to each pawn. Old reports stay under **Active**.

If your colony is gone by the time they come back, the squad stays together as a caravan where they left from.
