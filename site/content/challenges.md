---
summary: What each challenge reads from your colonists, and how the squad's scores add up.
card: Exp_HostileOutpost, Exp_ImperialTemple, Exp_TamingGrounds
---
## How a challenge scores a pawn

A challenge reads a few of a pawn's stats, or skill levels where the game has no stat. Each one is compared with what a decent pawn has, and that pawn scores 100. A pawn twice as good on a stat counts double for that stat, but no more, so one superstat can't carry a challenge alone.

Gear, bionics, traits, health and mood all go into the stats, as they do everywhere else in the game.

## How the squad's scores add up

- **Adds up**: every pawn's score is added, so more pawns help. Fight and haul work this way.
- **Weakest pawn**: the worst score counts. One loud pawn gives everyone away.
- **Best pawn**: the best score counts. One expert is enough.
- **Average**: the squad's average counts. A poor pawn lowers it and a good one raises it.

## Every challenge

{{ challenges_table() }}

A number in brackets after a stat is its weight: how much it counts against the challenge's other stats.

**Stealth** is capped at {{ tuning("loudStealthCap") }} after any loud arrival. **Haul** adds the shuttle's or vehicles' cargo room. A **search** challenge also decides the Find chance.
