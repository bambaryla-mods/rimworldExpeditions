# Expeditions: ideology update

Seven new opportunities that only show up when the colony's ideoligion would want them: a raider colony hears about a herd it could steal, a charitable one hears about starving refugees. Each one pays out the way vanilla counts that kind of deed, plus mood thoughts of its own.

## Intent
Ideology gives a colony wants: raids, charity, converts, sacrifices. Vanilla mostly answers them with full-scale actions. A raid means attacking a settlement, and charity means waiting for beggars or refugees to show up. Raiding is often a bad trade, and charity can't be sought out at all. Expeditions already has squads that go out and come back. This update gives each kind of ideoligion smaller, cheaper ways to act on what it believes, so two colonies with different faiths see different boards.

These are opportunities, not quests. Each one is something seen on the map or heard from travelers ("a neutral village's herd grazes far from its walls"), offered on the board like any other site. Nobody asks the colony to go.

## Goals
- Seven Ideology sites, each offered only when the colony's main ideoligion would want it.
- Deeds count the way vanilla counts them: a theft or a kidnapping is a raid, and alms are charity.
- Each site gives the squad, and where it fits the whole colony, mood thoughts of its own.
- Captives come home as slaves for a slaver ideoligion, or as prisoners for one that only sacrifices.
- The pieces are generic, so other mods can gate their own sites on precepts and memes and give thoughts the same way.
- Without Ideology, nothing changes and nothing errors.
- Old saves load, including squads already away.

## Non-goals
- Minority ideoligions. Only the colony's main one decides what is offered.
- Penalties for ignoring an offer, or for an ideoligion that would disapprove. A pacifist colony never sees the rustling offer, so it never has to turn it down.
- Changing what existing sites count as. Plague village, harvest and the other gentle jobs stay as they are (see Open questions).
- Relic hunts. Vanilla does those as quests.

## Anti-goals
- An offer that contradicts the colony's faith: rustling for a charitable pacifist, a heretic shrine for a faith with no supremacism.
- Captives arriving as plain colonists, or as slaves in a colony whose faith condemns slavery.
- A charity or raid credit that never reaches vanilla's precept moods because it fired the wrong event.
- Any Ideology def referenced in a way that breaks a game without the DLC.

## Approaches
1. **Generic pieces in Expeditions, sites as XML in its own Defs (my recommendation).** New gate, effect and find classes, plus seven site defs loaded only with Ideology. It follows how `Exp_WorkSite` is already marked `[MayRequireIdeology]`, and the pieces are reusable by other mods through the hooks from the mod-hooks update.
2. **A separate `Compat/Ideology` folder loaded through LoadFolders.xml.** It keeps Ideology content apart, but Expeditions already ships DLC sites in its main Defs, so it would be the odd one out.
3. **One worker class per site in C#.** Most of each site would be hard-coded. It's more flexible per site, but nothing would be reusable, and the XML-driven design the mod already has would go to waste.

Approach 1 is my call, as the user said only that these should be opportunities gated on the ideoligion.

## Design

### 1. Gating on the ideoligion
A new gate, `SiteGate_Ideo`, open when the player faction's main ideoligion (`Faction.OfPlayer.ideos.PrimaryIdeo`) matches:

- `anyPrecept`: open if the ideoligion has any of these precepts.
- `anyMeme`: open if it has any of these memes.
- `approvesSlavery`: open if any of its precepts has `approvesOfSlavery` (vanilla: `Slavery_Acceptable`, `Slavery_Honorable`).
- `anyRitualPattern`: open if it has a ritual built from any of these patterns (for `SacrificePrisoner`).

If several lists are set, any one match opens the gate. Two more lists close it whatever else matches:

- `noPrecept`, `noMeme`: closed if the ideoligion has any of these. Violent sites list the pacifist ones (VME's `VME_Pacifist` and `VME_Violence_Abhorrent`, Alpha Memes' `AM_NonViolence` and `AM_Violence_Abhorrent_Strict`), so a faith that mixes a raiding precept with a vow of nonviolence still never sees them.

`ClosedReason` names what's missing or what blocks it, for the debug evaluator. Without Ideology the gate is closed.

**Other DLCs and mods.** Every entry in these lists that comes from outside Ideology carries a `MayRequire` on its `<li>`, so the list just drops it when that DLC or mod isn't loaded:

| Source | MayRequire |
|---|---|
| Ideology | `Ludeon.RimWorld.Ideology` (on the site defs themselves) |
| Biotech | `Ludeon.RimWorld.Biotech` |
| Anomaly | `Ludeon.RimWorld.Anomaly` (note: the `Ritualist` meme is from Anomaly, not Ideology) |
| Odyssey | `Ludeon.RimWorld.Odyssey` |
| Alpha Memes | `Sarg.AlphaMemes` (but `AM_NonViolence` and `AM_Violence_Abhorrent_Strict` load only with VME as well: `Sarg.AlphaMemes,VanillaExpanded.VMemesE`) |
| Vanilla Ideology Expanded – Memes and Structures | `VanillaExpanded.VMemesE` |

The builder checks one point in game: a list with every entry dropped (Ideology only, no mods) must not leave a gate that matches nothing for a site that has no other entries. Every site below keeps at least one vanilla Ideology entry for that reason, except where noted.

`approvesSlavery` reads the precept flag, not a list of names, so any mod's precept that sets `approvesOfSlavery` counts automatically. None of the installed DLCs or meme mods add one today.

Gates already withdraw an offer when they close and leave a squad that's away alone. If the ideoligion is reformed and stops matching, the offer leaves the board and any squad already out finishes its trip.

### 2. Mood thoughts
- `PawnEffect_Thought`: gives each returning pawn a memory thought, per outcome. With `onlyIfFaithMatches`, it goes only to pawns whose own ideoligion passes the site's ideoligion gate, so a minority-faith squad member doesn't celebrate a heretic shrine burning. A pawn whose faith passes the gate for another reason (an iconoclast at the heretic shrine) celebrates too, which fits.
- `OutcomeEffect_ColonyThought`: gives every colonist at home a memory thought, with the same filter. It's for deeds the whole colony takes pride in, like a pilgrimage or a grove saved.

Each site gets its own thought defs: "rustled a herd", "gave alms", "made the pilgrimage", "preached to strangers", "humbled the heretics", "saved the grove". Numbers are left to the builder. They should sit near vanilla's own ideoligion memories, around +3 to +8 lasting a few days to two weeks.

### 3. Counting the deed the vanilla way
- **Raids.** `IdeoUtility.Notify_PlayerRaidedSomeone(raiders)` on Success or Partial, as settlement links already do. That feeds `Raiding_Required` and `Raiding_Respected`, their recent-conquest moods and the raid clock.
- **Charity.** Vanilla's charity precepts (`Charity_Worthwhile`, `Charity_Important`, `Charity_Essential`) react only to their own history events (`CharityFulfilled_Beggars` and so on), each through a `PreceptComp_KnowsMemoryThought`. We add a `HistoryEventDef` `Exp_CharityFulfilled_Alms`, patch each of the three precepts with a comp for it (an XML patch that runs only with Ideology), and add three thought defs, one per level, in vanilla's style. `OutcomeEffect_HistoryEvent` fires the event on the outcomes listed.
- **Conversion.** A convert who joins fires `ConvertedNewMember`, which the Proselytizer precepts already react to.

### 4. Captives
A new find, `CaptivesSpec` (like `JoinerSpec`: chance, count, kind, partial factor), for people who come home against their will. On arrival:

- If the main ideoligion approves of slavery, they arrive as slaves of the colony (`SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave)`) and fire the vanilla enslaved event so slaver precepts react.
- Otherwise, they arrive as prisoners, ready for a sacrifice.
- If the faith has both slavery and sacrifice, they arrive as slaves. A slave can still be sacrificed.

The status is decided on arrival, not at launch, so a faith reformed while the squad is away gets the right result. New slaves start with low suppression like any freshly enslaved pawn, and the colony has to manage them. That's part of the cost. Captives taken from a faction lower goodwill with it, the way vanilla kidnapping does. If the squad comes home as a caravan, the captives travel in it as prisoners or slaves.

### 5. Carrying the alms
The alms run is the one site where the squad must bring something. The site gets `supplies`: an amount of nutrition (any food the squad would eat) and a count of medicine.

- **Before launch.** The alms food is added to the trip's food need, so the food check and pickups cover both and the squad never sets off with alms it then eats on the way. Medicine is counted and picked up the same way as food: inventories first, then the nearest stock. `Departure.CanLaunch` refuses if either one is short.
- **At the site.** A Success or Partial gives the supplies away there. A Failure brings them home.
- **On the card.** The opportunity card shows the supplies, so the player sees them before picking a squad.

This is my proposal. A list of specific item defs was the first idea, but "food and medicine" isn't a def, and nothing else needs a list yet.

## The sites
Challenges and numbers are a starting point. The builder tunes them against existing sites.

Who sees each site, at a glance. The per-site sections below give the exact def names.

```
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Site            | Ideology                   | Other DLCs          | Alpha Memes             | VME Memes          | Never for            |
+=================+============================+=====================+=========================+====================+======================+
| Cattle rustling | Raider, Rancher            | -                   | Cowboys, Corsair        | -                  | Pacifists            |
|                 | Raiding Required/Respected |                     | Cattle Improved         |                    |                      |
|                 |                            |                     | Horses Desired          |                    |                      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Alms run        | Guilty                     | -                   | Monastic                | Egalitarian        | -                    |
|                 | Charity Worthwhile/        |                     |                         | Healthcare         |                      |
|                 |   Important/Essential      |                     |                         | Hospital Required  |                      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Pilgrimage      | Theist Embodied/Abstract   | Ritualist (Anomaly) | Jewish, Sikh, Jain,     | Shinto, Pantheism  | Atheism              |
|                 | Christian, Islamic,        | Nomadic Preferred   |   Kemetic, Mesoamerican | Exalted Priesthood | Religion Despised/   |
|                 |   Hindu, Buddhist origins  |   (Odyssey)         | Monastic                | Nomad              |   Disliked           |
|                 |                            |                     | Religion Loved          | Travel Desired     | Travel Despised      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Preaching tour  | Proselytizer               | -                   | -                       | -                  | Proselytizing Never  |
|                 | Proselytizing              |                     |                         |                    | Proselytism Disliked |
|                 |   Occasionally/            |                     |                         |                    |                      |
|                 |   Sometimes/Frequently     |                     |                         |                    |                      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Heretic shrine  | Supremacist                | -                   | Iconoclast              | Violent Conversion | Pacifists            |
|                 |                            |                     | Relic Destruction       | Proselytizing      |                      |
|                 |                            |                     |   Desired               |   Forceful         |                      |
|                 |                            |                     | Religion Despised       |                    |                      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Felled grove    | Tree Connection,           | -                   | Gauranlen Supremacy     | -                  | Pacifists            |
|                 |   Nature Primacy           |                     | Dryads Enhanced         |                    | Deforestation        |
|                 | Tree Cutting Prohibited/   |                     |                         |                    | Gauranlen Connection |
|                 |   Horrible/Disapproved     |                     |                         |                    |   Forbidden          |
|                 | Gauranlen Connection       |                     |                         |                    |                      |
|                 |   Strong                   |                     |                         |                    |                      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
| Captives        | Slavery Acceptable/        | Bloodfeeding,       | Sadist                  | Blood Court        | Pacifists            |
|                 |   Honorable (-> slaves)    |   Bloodfeeders      |                         |                    | Emancipation         |
|                 | Prisoner Sacrifice         |   Revered (Biotech) |                         |                    |                      |
|                 |   ritual                   | Inhuman, Psychic    |                         |                    |                      |
|                 | Cannibal                   |   Rituals Exalted   |                         |                    |                      |
|                 |                            |   (Anomaly)         |                         |                    |                      |
+-----------------+----------------------------+---------------------+-------------------------+--------------------+----------------------+
```

"Pacifists" is the list in section 1: VME's Pacifist meme and Violence Abhorrent, plus Alpha Memes' Vow of Nonviolence and its strict Violence Abhorrent.

In the "Shown when" lists, entries with no tag are vanilla Ideology. [Bio], [Ano] and [Ody] are Biotech, Anomaly and Odyssey; [AM] is Alpha Memes; [VME] is Vanilla Ideology Expanded – Memes and Structures. Each tagged entry gets the matching `MayRequire` from section 1.

### Cattle rustling (`Exp_Rustling`)
*A neutral village's herd grazes far from its walls.*
- **Shown when:**
  - Precepts: `Raiding_Required`, `Raiding_Respected`; [AM] `AM_Cattle_Improved`, `AM_Horses_Desired`.
  - Memes: `Raider`, `Rancher`; [AM] `AM_Cowboys` (frontier herders), `AM_Structure_Corsair` (pirate culture).
- **Never for:** the pacifist list (section 1).
- **Needs:** a neutral or friendly settlement nearby, a grassland-type biome.
- **Note:** for `AM_Cowboys` or `AM_Horses_Desired`, the herd should include horses when the biome has them.
- **Challenges:** Stealth (main), Tame or animal handling to drive the herd, light Fight.
- **Win:** 3–8 animals of a farm kind that fits the biome, come home tamed. Counts as a raid.
- **Cost:** goodwill with the village's faction drops on Partial or Failure (they were seen). On Success nobody knows.
- **Thought:** "rustled a herd" for the squad.

### Alms run (`Exp_AlmsRun`)
*Refugees are starving at a crossroads camp.*
- **Shown when:**
  - Precepts: `Charity_Worthwhile`, `Charity_Important`, `Charity_Essential`; [VME] `VME_Hospital_Required`.
  - Memes: `Guilty`; [VME] `VME_Egalitarian`, `VME_Healthcare`; [AM] `AM_Monastic` (ascetics giving away).
- **Note:** only the three Charity precepts react to the charity event. The other entries see the site and get the "gave alms" thought, nothing more.
- **Needs:** nothing on the map. The camp is between settlements.
- **Carries:** food and medicine (section 5). Something like 30 meals' worth of nutrition and 5 medicine, tuned by the builder.
- **Challenges:** Endure, Haul, a little Teach or medicine for the sick.
- **Win:** charity credit (section 3), goodwill with every friendly faction, a chance a refugee asks to join.
- **Thought:** "gave alms" for the squad.

### Pilgrimage (`Exp_Pilgrimage`)
*Travelers speak of an old shrine of our faith three days north.*
- **Shown when:**
  - Structures: `Structure_TheistEmbodied`, `Structure_TheistAbstract`, the four `Structure_Origin*` (Christian, Islamic, Hindu, Buddhist); [AM] `AM_Structure_Jewish`, `AM_Structure_Sikhism`, `AM_Structure_Jainism`, `AM_Structure_Kemetism`, `AM_Structure_Mesoamerican`; [VME] `VME_Structure_Shintaoism`, `VME_Structure_Pantheism`. Pilgrimage is a real practice in each of these.
  - Memes: [Ano] `Ritualist`; [AM] `AM_Monastic`; [VME] `VME_ExaltedPriesthood`, `VME_Nomad` (travel for its own sake).
  - Precepts: [AM] `AM_Religion_Loved`; [VME] `VME_Travel_Desired`; [Ody] `Nomadic_Preferred`.
- **Never for:** [AM] `AM_Structure_Atheist`; [AM] `AM_Religion_Despised`, `AM_Religion_Disliked`; [VME] `VME_Travel_Despised`.
- **Needs:** nothing on the map. The length is in a long stay at the shrine (`daysOnSite` 3), not a minimum distance, which could exceed a player's range setting.
- **Note:** a travel-loving faith with no gods (`VME_Nomad`, `VME_Travel_Desired`, `Nomadic_Preferred`) sees the same site. The builder could give it a secular label ("a long journey to a famous place"), or keep one label for everyone. My call: one label, with the thought text kept neutral ("made the journey").
- **Challenges:** Endure (main). Long `daysOnSite`.
- **Win:** a long mood buff for the pilgrims, a short one for the colony, certainty up for the pilgrims. If the ideoligion is fluid, a development point.
- **Thought:** "made the pilgrimage" for the squad, "our pilgrims returned" for the colony.

### Preaching tour (`Exp_PreachingTour`)
*A village of another faith is open to visitors.*
- **Shown when:**
  - Memes: `Proselytizer`.
  - Precepts: `Proselytizing_Occasionally`, `Proselytizing_Sometimes`, `Proselytizing_Frequently`.
- **Never for:** [VME] `VME_Proselytizing_Never`; [AM] `AM_Religion_ProselytismDisliked`.
- **Note:** VME's `VME_Proselytizing_Forceful` and `VME_ViolentConversion` go to the heretic shrine instead. A faith that converts by force doesn't go door to door.
- **Needs:** a neutral or friendly settlement of a faction with a different main ideoligion.
- **Challenges:** Debate or social (main), Endure.
- **Win:** goodwill with that faction, certainty up for the preachers, a chance a convert joins (fires `ConvertedNewMember`).
- **Cost:** Failure costs goodwill. They didn't like being preached at.
- **Thought:** "preached to strangers" for the squad.

### Heretic shrine (`Exp_HereticShrine`)
*Pilgrims of a rival faith gather at a shrine near a hostile settlement.*
- **Shown when:**
  - Memes: `Supremacist`; [AM] `AM_Iconoclast` (holy images must be destroyed); [VME] `VME_ViolentConversion`.
  - Precepts: [AM] `AM_RelicDestruction_Desired`, `AM_Religion_Despised`; [VME] `VME_Proselytizing_Forceful`.
- **Never for:** the pacifist list (section 1).
- **Note:** for an iconoclast or religion-despising faith, any other faith counts as rival, even one that shares memes with ours. The supremacist check ("isn't ours") stays as written.
- **Needs:** a hostile settlement nearby whose faction's main ideoligion isn't ours.
- **Challenges:** Fight (main), Stealth.
- **Win:** goodwill drops with that faction, a little loot (the shrine's offerings), counts as a raid.
- **Thought:** "humbled the heretics" for the squad and the colony, filtered to pawns who hold Supremacist.

### Felled grove (`Exp_FelledGrove`)
*Loggers are clearing an ancient grove.*
- **Shown when:**
  - Memes: `TreeConnection`, `NaturePrimacy`; [AM] `AM_GauranlenSupremacy`.
  - Precepts: `TreeCutting_Prohibited`, `TreeCutting_Horrible`, `TreeCutting_Disapproved`, `GauranlenConnection_Strong`; [AM] `AM_Dryads_Enhanced`.
- **Never for:** the pacifist list (section 1); [AM] `AM_Deforestation`, `AM_GauranlenConnection_Forbidden`.
- **Needs:** a forested biome.
- **Challenges:** Fight (main), Endure.
- **Win:** a strong colony mood buff. A Gauranlen seed comes home on a full Success.
- **Thought:** "saved the grove" for the colony, filtered to pawns who hold one of the two memes.

### Captives (`Exp_Captives`)
*A nomad band camps with no guards posted.*
- **Shown when:**
  - Slavery: `approvesSlavery` (vanilla `Slavery_Acceptable`, `Slavery_Honorable`, and any mod precept with the flag).
  - Rituals: a `SacrificePrisoner` ritual.
  - Memes that want a prisoner for something: `Cannibal` (meat); [Bio] `Bloodfeeding` (hemogen); [Ano] `Inhuman` (psychic rituals that need a target); [AM] `AM_Sadist`; [VME] `VME_BloodCourt`.
  - Precepts: [Bio] `Bloodfeeders_Revered`; [Ano] `PsychicRituals_Exalted`.
- **Never for:** the pacifist list (section 1); [VME] `VME_Emancipation`.
- **Note:** only `approvesSlavery` makes them slaves. Every other reason brings them home as prisoners, which is what each of those faiths wants them for.
- **Needs:** any non-player faction nearby (a new `FactionNeed.Any`), the source of the nomads. Captives are generated as its members.
- **Challenges:** Stealth (main), Fight.
- **Win:** 1–3 captives (section 4), as slaves or prisoners. Counts as a raid.
- **Cost:** goodwill with the nomads' faction.
- **Thought:** none of our own. Vanilla's slavery and raid moods cover it.

## Saves
All new: site defs, gate, effects, find, thoughts, history event. An old save just starts offering them. `CaptivesSpec` is a new optional field, so expeditions saved before the update don't have it. An Ideology site removed by the player in a later version follows the existing missing-def handling for offers and squads.

## Left to the builder
- Every number: challenge requirements, weights, squad sizes, loot, thought strengths and durations, alms amounts.
- Text: descriptions, report lines, thought labels, in the style of existing sites.
- Which biomes and terrain each site needs beyond what's listed.
- Where the alms pickup code lives, as long as it shares logic with food pickup.

## Open questions
- Are these seven the right set? The user hasn't confirmed all of them or named any to drop.
- Should existing gentle jobs (plague village, harvest, rebuild, teach a village) also count as charity? It would be a small change and would help charitable colonies, but it changes existing sites.
- Should existing sites that contradict a faith be hidden from it? A slaver colony is offered the slaver convoy, which frees slaves for an ally. The same gate could hide it from `approvesSlavery` faiths and give [VME] `VME_Emancipation` faiths a "freed the chained" thought on it.
- Existing sites that installed memes would cheer, through thoughts only, without gating: plague village for [VME] `VME_Healthcare` and `VME_Hospital_Required`; melee bouts for [AM] `AM_Gladiator`, [VME] `VME_Bushido`, `VME_BloodCourt`, `VME_Violence_Honorable`.
- Memes with no site yet that could get one later:
  - A sanguophage lair for [AM] `AM_VampireHunting`, `AM_HuntFocus_*`, `AM_SanguophageCamps_RaidingDesired`, and [Bio] `Bloodfeeders_Reviled`.
  - A deep cave for `Tunneler` and [AM] `AM_DeepDevotion`.
  - Something with water for [AM] `AM_WaterPrimacy`.
