# Expeditions: Design

Standalone RimWorld 1.7 mod: **Expeditions** (`bambaryla.expeditions`). This document lives in BOR's `openspec/` until the mod has its own folder; it moves there when work starts.

## Intent
Let a colony go looking for things instead of buying them. The player picks a point of interest (PoI) on the colony's own flat map, sends a squad as they stand, and the squad is gone for the trip. The result is rolled off-screen and comes home as a report: what they found, what they could carry, who got hurt, who did not come back. Sometimes they find nothing.

Inspired by the idea of Stellar Logistics (order, wait, receive), but questing instead of buying.

## Goals
- Squads leave and return on their own; the player never manages them on the world map.
- Outcomes come from vanilla stats, so gear, skills, bionics and traits all matter, and modded items count without extra work.
- Every factor in an outcome is visible to the player before launch.
- No single best squad: different PoIs reward different strengths.
- Content is XML: other mods (BOR first) add PoI types, challenges and loot.

## Non-goals
- Real maps at PoIs. Everything at the destination is resolved off-screen.
- Anything on the vanilla world map. PoIs and squads exist only on the mod's own map.
- Faction territories.
- BOR content and the Schism. BOR plugs in later through its own compat folder (see *Later*).

## Anti-goals
Failure even if it technically works:
- "More gun is better" turns out to be the best strategy everywhere.
- The player has to babysit squads or the world map.
- Outcomes feel random because the player cannot see why.
- Any code from Stellar Logistics or BambaMap, or any dependency on BambaMap. Neither is the author's to publish.
- A hard dependency on BOR, Vehicle Framework, or UI Rethemed.

## Constraints
- RimWorld 1.7 only, Harmony, written from scratch.
- Vehicle Framework and [LFS] UI Rethemed are optional and detected at runtime.
- Opening the window must feel instant after the first bake.

## Approaches
Decided with the requester:

| Question | Chosen | Rejected, and why |
| --- | --- | --- |
| What happens at the PoI | Resolved off-screen, with a report | A real map: scope grows enormously |
| Where PoIs and squads live | Only on the mod's own map | Vanilla world objects: more work, clashes with other world-map mods; can be added later on top |
| Entry point | Comms console | A new building: not wanted |
| Away pawns | The mod's own holder plus a short list of patches | A hidden vanilla caravan: vanilla and modded caravan events (ambush, demands) would fire on it |
| Map drawing | Bake a texture once from world tile data; draw markers live | Vanilla world camera: locks the look to vanilla, heavier, exposed to world-map mods |
| Map style | Vanilla and Tactical holo, switchable | Cartographer: liked, but needs too much art; the renderer leaves room to add it later |
| Window layout | Layout A, map first | Split, board first |

## Design

### Entry: the comms console
A powered, player-owned comms console (vanilla or modded, anything that is a comms console) SHALL offer an **Expeditions** gizmo that opens the window. Opening it needs no pawn. Prospecting is a job at the console (see Prospecting). Without a powered console, running expeditions continue and come home, but nothing new can be launched or prospected.

The window's home tile is the tile of the map the console stands on.

### The window (layout A)
```
+----------------------------------------------------------------+
| EXPEDITIONS   [Opportunities] [Prospect] [Active] [Captured]  X|
+-----------+----------------------------------------------------+
| list      |                                                    |
|           |                  flat map                          |
|           |       (home centered, range ring, markers)         |
|           |                                [Vanilla|Holo]      |
|           +----------------------------------------------------+
|           | details drawer: PoI | challenge bars | squad, ride  |
+-----------+----------------------------------------------------+
```
- **Opportunities:** ready-made and prospected PoIs. Selecting one in the list or on the map opens the drawer.
- **Prospect:** item picker and the prospecting state (see Prospecting).
- **Active:** squads out, with their phase (outbound, on site, returning), time left, and portraits. Selecting one highlights its path on the map.
- **Captured:** every colonist held by another faction, with a Rescue action (see Captives).
- A **History** list of the last 20 reports, reopenable, MAY sit under Active.
- The drawer shows the PoI (name, place, distance, known hostiles), one bar per challenge (squad score against the PoI's requirement), the squad slots, the ride picker, and three numbers: **Success**, **Find**, and **Away** (days). Hovering a bar lists each contribution: pawn, stat, and what moved it (for example "Ann: Shooting accuracy 94% (bionic eye +12%)").
- The style toggle (Vanilla / Holo) sits on the map and is remembered in the mod settings.

Mockups: `expedition-layouts.html` (layout A) and `expedition-map-styles.html` (styles 1 and 2), from the brainstorm session.

### The flat map
- **Projection:** azimuthal equidistant around the home tile, out to the expedition range (default 60 tiles, a mod setting). Distance on the map then matches distance in the world.
- **Bake:** gather the tiles in range by walking tile neighbours from home, project each tile center, and fill a texture (nearest tile per pixel). Store per tile: biome, elevation, hilliness, rivers, roads, coast. Cache the baked texture per style until the world or the home tile changes.
- **Vanilla style:** biome colors as on the world map (per-biome color from a table, falling back to the average color of the biome's world texture), hill shading, river and road lines, white markers.
- **Holo style:** dark background, a faint grid, coastline outline, elevation contour lines, biome as a tint, glowing markers, and a faint scanline.
- **Live layer, drawn each frame:** home, faction settlements in range (context only), PoIs, squads and their paths, captive locations, and the range ring. Pan and zoom within the baked area.

### Content defs (XML)
- **`ExpeditionChallengeDef`:** a label, a list of `{stat, weight, reference}` entries, and how the squad combines: `Sum`, `Min` (weakest pawn), `Max` (best pawn) or `Average`. Shipped set: Fight (Sum), Stealth (Min), Search variants per target (Max), Haul (Sum), Endure (Average). The mod MAY define its own StatDefs where vanilla has none, computed by a `StatWorker`.
- **`ExpeditionSiteDef`:** label, description, icon, where it can appear (biomes, hilliness, river, road, coast, an owning faction of a given relation within range), its challenges with weights and requirement levels, loot (a loot table keyed by thing categories or a `ThingSetMakerDef`), a risk profile (odds of injury, death and capture for each outcome), time on site, and which thing categories it serves for prospecting.
- **`ExpeditionExclusionDef`:** items that can never be prospected, on top of the built-in rules.

### Ready-made opportunities
Up to 5 at a time. A new one appears every 1-3 days while there is room, and each expires 3-8 days after it appears. Each comes from a site def whose conditions match a tile in range, so the board reflects what is around: a mine in the mountains, a wreck in the tundra, a hostile faction's outpost nearby, a friendly faction's job paid in goodwill and silver. Loot fits the site and, for faction sites, that faction's tech and gear.

### Prospecting
- **Picker:** a searchable list of every item def, except corpses, minified buildings, quest-only items, untradeable items, items with no market value, and the exclusion defs. Each row shows the chance of finding a lead.
- **Chance of a lead:** falls with the item's `techLevel` and `BaseMarketValue`, through curves defined in XML. Steel is near certain; an archotech item is a long shot.
- **Job:** a colonist works at the comms console. Duration scales with a vanilla stat (`NegotiationAbility`, falling back to `ResearchSpeed`). The stat changes speed, never odds. Progress is saved per requested item. If the job is interrupted, any colonist at any console continues from where it stopped.
- **Seed:** a world-wide seed changes every 2.5 days. The seed and the item decide whether a lead exists, so reloading or retrying cannot change the answer. The Prospect tab shows when the seed next changes.
- **Repeat:** asking for an item already prospected under the current seed says "No new information available yet" before the job starts.
- **Lead found:** a PoI appears from a site def that serves the item's category, with the item guaranteed in its loot. **Nothing found:** a short report.
- Finding a lead is not finding the item: the squad must succeed, pass the Find roll, and carry it home.

### Squad and ride
- The squad is picked from colonists on the console's map who are not downed, in a mental state, prisoners, guests, or already away. They go as they stand: equipped gear and inventory are what they have.
- **Rides:**

| Ride | Available when | Out | Back | Carrying | Arrival |
| --- | --- | --- | --- | --- | --- |
| Foot | Always | Walking speed | Walking | Pawns' and pack animals' carrying capacity | Quiet: Stealth counts in full |
| Drop pods | Enough pods with launchers and fuel on the map | Fast; pods used up | Walking | As on foot, for the walk home | Loud |
| Shuttle | A player-owned shuttle on the map with fuel for the trip | Fast | Shuttle | Shuttle cargo | Loud |
| Vehicles | Vehicle Framework, and enough seats for the squad | Vehicle speed | Vehicles | Vehicle cargo | Loud; armed vehicles add to Fight with variance |

- **Loud** arrival caps the Stealth challenge. An armed vehicle's Fight bonus is multiplied by a per-site terrain factor and a random spread, so a tank helps at an open camp and does little in a ruin.
- **Travel time** comes from vanilla world pathing between the home tile and the PoI tile, at the ride's speed (for Foot, the vanilla caravan speed of the slowest member).
- **Departure:** the squad walks to the map edge (Foot), boards the pods, shuttle or vehicles, and leaves. Then the pawns, any pack animals and vehicles go into the holder. Pods and their fuel are consumed.

### Resolution
On arrival at the PoI:
1. **Challenges:** for each challenge, the squad score (stats combined by the challenge's rule, plus ride modifiers) is compared with the site's requirement. The scores combine by weight into **Success**, clamped between 5% and 95%.
2. **Outcome:** Success, Partial or Failure.
3. **Find:** a separate roll, driven by the Search challenge, decides whether the target or loot is there. "We got in and out, but found nothing" is a valid result.
4. **Loot:** rolled from the site's table (full on Success, reduced on Partial, none on Failure), then cut to what the ride can carry home, keeping the most valuable per kilogram.
5. **Pawns:** each pawn rolls on the site's risk profile for the outcome: unharmed, injured, killed, or captured. Injuries are real hediffs. Gear loses hit points by danger. Survivors gain experience in the skills behind the challenges they took part in.
6. **Report:** written now, shown when the squad gets home. Deaths are reported at once by letter.

### Return
Survivors and loot travel back by the ride's return mode. On arrival they enter the map they left from: on foot or by vehicle from the map edge, by shuttle landing near the console. If that map is gone, they go to another player home map. A letter announces the return and opens the report.

### Captives
- **Tracked:** the Captured tab lists every colonist held by another faction, with the captor, how long they have been held, and where. This covers pawns captured on expeditions and vanilla kidnappings, by reading and adding to vanilla's `KidnappedPawnsTracker`. Vanilla's own handling (selling, joining raids) then applies to all of them.
- **Rescue:** each captive has a Rescue action that creates a PoI at the captor's real settlement if one is in range, or at a generated holding camp in range if not (for factions without settlements, or none in range). A rescue leans on Fight and Stealth. On success the captive comes home with the squad.

### Away pawns (the holder)
A game component holds away pawns, animals and vehicles in a `ThingOwner`. Patches keep them counted as the colony's:
- The colonist bar shows them as away, grouped by expedition.
- Colonist counts and the game-over check include them, so a colony with everyone away is not over.
- Relations, ideology roles and similar keep treating them as colony members.
- Away pawns and animals are frozen while away (no needs, no aging, no healing), like a pawn in cryptosleep. Food is still paid for (see Food).

### Food
- A squad SHALL carry enough food for the whole trip: out, on site, and back. The need is each member's daily nutrition (pawns and pack animals, each by its own diet) times the trip's days for the chosen ride.
- The drawer shows food needed against food available. Launch is disabled, with a reason, while it falls short.
- Food counts from the squad's inventories first, then from the map's stockpiles. On departure the squad picks up the stockpile food it needs on the way out, as vanilla caravan forming does.
- Food does not rot while away (everything is frozen).
- When the squad comes home, the needed amount SHALL be removed from what they carry. Anything beyond it comes back with them. Food for members who died or were captured is removed too, since it was eaten or lost on the way.

### Mod settings
Map style, expedition range, and the ready-made count and timings.

### Integrations
- **Vehicle Framework:** detected at runtime; the Vehicles ride appears only when it is active.
- **[LFS] UI Rethemed:** a separate assembly loaded through `LoadFolders.xml` when it is active, drawing the window in its theme, as in CommRecruiter.

### Persistence
Saved with the game: offers, prospect progress and the prospected set for the current seed, squads and their phase, the holder's contents, pending reports, history, and rescue PoIs.

### Debug
Dev-mode actions: spawn an offer, finish a prospect, jump a squad to its next phase, force an outcome, and re-bake the map.

## Left to the builder
- How tiles are gathered and projected, and the pixel fill method, as long as opening is instant after the first bake.
- Curves and numbers: lead chance by tech level and value, challenge requirements, risk profiles, loot amounts, slot and timing defaults. All of them in XML.
- Which shuttle defs count (for example Odyssey's passenger shuttle), found by comp rather than by name.
- How injuries are applied (damage worker hits or direct hediffs), as long as they look like real fight wounds.
- The exact patch list for the holder.

## Later (not in this design)
- **BOR compat** (`Mods/Expeditions` in BOR, loaded when this mod is active): Solar, Kwazaari and Tenebral site defs and loot.
- **Schism** (BOR): rebuilt on top of this mod, with nemesis camps and the champion's stronghold as PoIs. See `openspec/specs/schism/spec.md`, which predates this design.
- A Cartographer map style.
- PoIs and squads as vanilla world objects.
