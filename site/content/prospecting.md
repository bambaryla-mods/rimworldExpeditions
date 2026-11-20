---
summary: Ask around on the comms for any item. The rarer and more advanced it is, the less likely anyone knows.
card: Exp_Rumor, Exp_CrashedShip, Exp_MilitaryDump
---
## Asking around

On the **Prospect** tab, pick a mod (Core is the default), type at least three letters, and choose an item. A colonist then works at the comms console asking around. A good negotiator asks faster; it doesn't change the odds.

If they are interrupted, anyone at any console picks up where they stopped.

## The odds

The chance that somebody knows where to find an item is its tech level factor times its market value factor:

{{ tech_curve() }}

{{ value_curve() }}

Steel is close to certain. An archotech item is a long shot. The tab shows the chance for every item before you ask.

## Rumors change every 2.5 days

The answer for an item is fixed until the rumors change, every 2.5 days. Asking again in the meantime, or loading a save, gets the same answer: "No new information available yet." The tab shows how long until the next change.

## Leads

A lead puts a site on the map that fits the item: a mine for ore, a convoy for textiles, a military dump for weapons. The item is in that site's loot for sure.

A lead isn't the item yet. The squad still has to succeed, find it, and carry it home. When nothing fits, the lead is a {{ site_link("Exp_Rumor") }}.

A prospected lead stays on the board for {{ tuning("prospectLifetimeTicks", "days") }} days.

Some items can never be prospected: corpses, buildings, quest items, and things nobody trades.
