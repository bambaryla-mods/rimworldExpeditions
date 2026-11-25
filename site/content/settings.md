---
summary: The mod settings, and the mods Expeditions works with.
card: Exp_AmmoDump, Exp_MechanitorComplex, Exp_ImperialTemple
---
## Settings

In the mod settings you can change:

- **Map style**: Vanilla looks like the world map. Holo is a dark map with contour lines and glowing markers.
- **Expedition range**: how far out sites can turn up, from 20 to 120 tiles. The default is 60.
- **Opportunities at a time**: how many ready-made opportunities the board holds, from 1 to 10. The default is 5.

## What you need

{{ requirements() }}

## Other mods

- **Vehicle Framework**: adds vehicles as a ride. Armed vehicles can help in a fight. See [Rides and food](rides.html).
- **[LFS] UI Rethemed**: the Expeditions window is drawn in its theme.
- **Combat Extended**: adds the {{ site_link("Exp_AmmoDump") }}.
- **Royalty**: adds the ruined imperial temple, and its outposts and mech clusters with a machine harming your colony show up on the board.
- **Biotech**: adds a site with a mechlink, and its ancient mechanitor complex shows up on the board.
- **Ideology**: its work sites and ancient complexes show up on the board.
- **Odyssey**: its gravcore sites, ancient structures, ancient mercenaries and alpha thrumbo show up on the board, and game herds also turn up in its grasslands.
- **[Beyond Our Reach](https://bambaryla-mods.github.io/RimworldBeyondOurReach/integrations.html#expeditions)**:
  - eleven sites of its own, each waiting for its tier's research or the colony's path:
    - tenebral ruins, a second way to the tier III artifacts;
    - Umbral hunting grounds;
    - a failing neutronium store;
    - Kwazaari tech recovery for scrip;
    - crusades, retreats and Umbral hunts on the Path of Light;
    - reaping and dark pact errands on the Path of Dark.
  - It also changes some sites here:
    - the Solar Republic pays in its goods;
    - Badzaari and Vanquisher outposts hold their own gear;
    - a {{ site_link("Exp_DownedCourier") }} can carry the data drive;
    - {{ site_link("Exp_DormantMechs") }} can be its work mechs.
  - Its sites are listed on its own site, not here.

The DLC quest sites are on [Places to search](finds.html#ideology-work-sites). Sites in orbit aren't included.

## For modders

Another mod can add its own sites without touching Expeditions' code. Everything below is XML on a site def, or a small class the other mod ships:

- **Waiting for something**: `gates`, conditions a site waits for. Research is built in (`SiteGate_Research`), and so is a mechanitor. Your own is a subclass of `SiteGate`. Set `immediate` for a site that answers something with its own clock, like a quest: it's offered the moment its gates open, and a gate can set its deadline.
- **Loot**: an entry can wait for research (`research`) or stop dropping once its item has been analysed (`untilAnalyzed`), for key artifacts the player can find another way.
- **Factions**: `factionDefs` and `enemyFactionDefs` pick whose settlements a site is near. An `ExpeditionFactionDef` lets a faction pay in its own goods, add loot to named sites when it holds them, or add effects there.
- **Real settlements**: `settlementLink` offers a site for a real settlement that a win raids or razes. A `SettlementLinkWorker` subclass can narrow which settlements, call the squad back, or set the loot.
- **Effects**: `pawnEffects` run on each pawn who came through, and `outcomeEffects` run once, at the site or when the squad gets home. An `ExpeditionLootFilter` subclass can strike items out of every haul.
- **Joiners**: `partialFactor` lets them come on a Partial too. `mechKinds` makes them mechs for a mechanitor in the squad.

Beyond Our Reach's `Mods/Expeditions` folder is a working example of all of these.
