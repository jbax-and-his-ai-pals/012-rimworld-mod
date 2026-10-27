# Roadmap

Legend: `[x]` done, `[~]` in progress, `[ ]` planned. Each phase builds on the one before; tasks inside a phase can usually be done in any order.

## Phase 0: the ruins (done)

- [x] Building planner: campuses of buildings, hallways, fire doors, stepped and chamfered footprints, atria, suites, interlocks, link corridors, filled courtyards, isolated buildings; layout styles (campus, scattered, compound); sizes from 60 cells up to the whole map
- [x] Sector graph carved along real doors, with gates, a solvability simulation and a gated layout mode
- [x] Six complex types as world sites with their own quests, loot tables, outdoor props and accent colors
- [x] Furniture, dust, preserved rooms, containers, cryosleep caskets with survivors, secure rooms, rec rooms and specialty rooms
- [x] Scenario start, starting gear by skill, mod settings window, safe start sweep, test tooling (hero mode, hotkeys, launch flags)

## Phase 1: terminals and security

- [x] **1.1 Cipher-lock minigame logic**: a pure, tested puzzle (candidate codes, positional-match feedback, attempts, decoy purges, lockout), difficulty from the hacker's Intellectual skill
- [x] **1.2 Minigame window and job**: a game window driven by the logic; the hack job opens it on arrival; success, failure and lockout apply to the terminal
- [~] **1.3 Security groups**: turrets with a dormant state, placed in sensitive rooms away from the start and grouped per building (groups and droids done in the gated layout; open-layout placement and standby mode still to do)
- [~] **1.4 Security consoles**: a console in a nearby security office switches its group between hostile, standby, allied and disabled, gated by the minigame
- [ ] **1.5 Settings and docs** for density and default states

## Phase 2: the enclave

- [x] **2.1 Enclave site type**: a friendly settlement laid out from the town plan, with a safe approach (walls come from the plan's wall projects)
- [~] **2.2 Residents**: a hidden faction, neutral then allied by standing, villagers under a lord (guards and daily routines still to do)
- [ ] **2.3 Services**: trader, canteen, clinic, notice board that offers the ruin quests
- [~] **2.4 Standing system**: standing rises daily while present and gates growth (deliveries, cleared sites, ranks and a readout still to do)
- [x] **2.5 Visitable enclave quest** on the world map

## Phase 3: the living town

- [x] **3.1 Town plan generator**: an ordered list of projects (houses, stalls, walls, gardens) laid out around a plaza (pure and tested)
- [~] **3.2 Staged builders**: villagers walk to a site and the building appears in stages (tiles appear over time; villagers do not yet walk to the site)
- [x] **3.3 Lots and reconciliation**: planned lots are revealed as the town decides; the town never builds on cells the player has claimed or used; the player can claim lots
- [x] **3.4 Growth driven by standing**
- [ ] **3.5 Lodging ladder**: cot, room, apartment, suite, with furniture allowed inside your own lodging
- [ ] **3.6 "Delver in Residence" scenario**: start as a lodger in the enclave, opt-in

## Phase 4: the wider world

- [ ] **4.1 Data cores and clues** as loot that feed research and point to other sites
- [ ] **4.2 Questline** chaining several complex types, with the enclave as quest giver
- [ ] **4.3 Danger ratings and reward scaling** by colony wealth
- [ ] **4.4 Claim and restore**: the enclave starts at a complex's front door on the same map and settles cleared sectors (see BACKLOG)

## Delve mode (next milestone)

A one-shot or long-haul run where each complex tile is a level. Reach the extraction gate, step through, arrive at the next complex. The party's death ends the run.

- [ ] **D.1 Difficulty by depth**: a pure, tested table from depth to complex size, type weights, security density, hostile casket share and loot richness
- [ ] **D.2 Extraction gate**: a building placed far from the start, unlocked by a cipher-locked terminal, that carries the party and what they hold to the next level
- [ ] **D.3 Level transition**: generate the next complex, warp the party, retire the old map
- [ ] **D.4 Run state and scenario**: depth counter, a "Site-44 Delve" scenario, best-depth record, end-of-run summary
- [ ] **D.5 Between levels**: a short rest or supply stop (what carries over, what does not), tuned in play

## Phase 6: the world layer (after the enclave work)

- [ ] **6.1 Expedition contracts** offered by the enclave: retrieve, rescue, clear, timed
- [ ] **6.2 Survival layer** on top of delve mode: the outside world, towns, goals and progression that cater to delvers

## Morale and kit (done, to tune)

- [x] Mission focus mood lift, delver tolerance for plain corpses, focus tabs, delver cap and vest, starting kit and loot frequencies as settings

## Phase 5: depth and polish

- [ ] **5.1 Sequential dungeon layout**: the gated, linear variant
- [ ] **5.2 Type set pieces and hazards**: armory vault, furnace hall, contamination, containment breaches
- [ ] **5.3 Care stations** for fast healing; recovery tuned for expeditions
- [ ] **5.4 Persistent sites** you can revisit
- [ ] **5.5 Fog gates** so C-shaped rooms can be revealed gradually
- [ ] **5.6 Translation keys** for all player-facing text
- [ ] **5.7 Release polish**: preview image, Workshop page, changelog

See also [BACKLOG.md](BACKLOG.md) for smaller open items.
