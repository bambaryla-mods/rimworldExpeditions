---
summary: Mines, wrecks, hives, digs, vaults and the DLC quest sites, what they ask of a squad and what they hold.
card: Exp_AbandonedMine, Exp_CrashedShip, Exp_HostileOutpost, Exp_MechCluster, Exp_Dig
---
## Every site with something to take

{{ sites("finds") }}

How to read it:

- **Challenges**: the number is what the site asks for; a decent pawn scores 100, and on a challenge that adds up, three decent fighters make about 300. The share is how much each challenge counts toward Success. See [How it works](guide.html#success).
- **What's there**: the full haul on a Success. A Partial brings back about half, a Failure nothing. A percentage is the chance that item is there at all.
- **Chance it's there**: the Find number. With a search challenge it runs from the low end with a poor searcher to the high end with an expert.
- **Risk per pawn**: what each pawn's roll can bring, by outcome. Whatever is left is "unharmed".

A site marked with a DLC or mod only turns up when you have it. "Rare" sites turn up less often than the rest.

## Ideology work sites

With Ideology, the camps other factions set up to farm, log, mine or hunt show up on the board too, as long as they are in range. You can still raid one yourself and take the whole stash, or send a squad and let them bring back up to {{ tuning("worksiteShare", "pct") }} of it. A Partial brings back half of that, and a Failure nothing.

The guards decide how hard the fight is: every guard adds {{ tuning("worksiteFightPerGuard") }} to the [fight](challenges.html#fight) the squad needs, with 100 as the minimum, so the fight number in the table below is only an example. Nothing is listed under "What's there" because each camp's stash is its own. The offer shows how many guards there are, the most the squad can bring home (a small squad on foot may still have to leave some behind), and when the camp leaves. A squad that would arrive too late can't be sent.

A squad's raid counts as yours. The owner turns hostile if it wasn't already (a small independent group always is), and the camp is gone afterwards. On a Success or Partial it also counts as a raid for an ideoligion that cares about raiding. If the camp packs up, or you raid it yourself while the squad is on the road, they turn back and come home with nothing.

{{ sites("worksites") }}

## DLC quest sites

Quest sites from Royalty, Biotech, Ideology and Odyssey show up on the board while their quest runs and the site is in range. That covers the ancient complexes and the mechanitor complex, Odyssey's gravcore sites, ancient structures, ancient mercenaries and the alpha thrumbo, and Royalty's outposts and mech clusters that carry a machine harming your colony. Sites in orbit aren't included: squads only travel on the ground.

These sites have no cut. A Success brings back the whole prize, or destroys the machine so its effect wears off. A Partial does the same, but whoever held the site saw the squad leave: about half a day after they get home, a raid comes for the colony at half the usual strength. Mechs answer for mech clusters, the complexes, the ancient structures and most gravcore sites. Insects answer for their nests, raiders for the mercenaries and for pirate outposts, and enraged animals for the alpha thrumbo. A Failure brings nothing back. A site with a prize is lost along with its quest, but a site that is harming you stays where it is and can be tried again.

The fight follows the site's own threat, or the threat a site made today would have when its quest didn't set one, so the fight numbers in the table below are only examples. Most of these sites only fill up with loot when the game makes their map, so the haul here is a table the mod keeps for each one. The complexes also roll vanilla's own room loot, and the gravcore sites bring the gravcore their quest names. Mechs, insects and the ancients don't take prisoners.

{{ sites("worldsites") }}
