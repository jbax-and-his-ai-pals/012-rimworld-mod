# Site-44 backlog

Status legend: `[ ]` open, `[~]` in progress, `[x]` done.

## Open

The plan for the first published build is in BETA.md. Enclave and town work is parked in ARCHIVE.md.

- [ ] **Building security with security terminals.** Turret groups in sensitive rooms (armory, vaults, server rooms, containment, near secure doors), each controlled from a security console in a nearby security office. Modes: hostile to intruders, standby (ignores the party, still fires on raiders), allied (fires on the party's enemies), disabled. Turrets are placed deep and away from the start; default state varies. Builds on the facility faction relation machinery and a dormancy component on the turrets.
- [ ] **Terminal hacking minigame (cipher lock).** A cipher-lock puzzle in a window: pick the correct access code from a list of candidates, with positional-match feedback, limited attempts and decoy purges. Hacker's Intellectual skill sets candidates, attempts and purges; failure locks the terminal for a while. Used for gate, security and vault terminals, as an alternative to the timed hack. Further puzzle styles later (circuit routing, timing).
- [ ] **Sequential dungeon layout** (filed from the "layouts" discussion). A second layout mode beside the current open ruins: a more linear building or dungeon with a clear progression, built on the gated sector graph (terminal gates, clearance tiers, hazards, coolant valve, reactor). The open layout is the default; this one is an alternative. Reuses the `FacilityGraph` tree, gates and `Site44Tuning.OpenLayout = false`; needs its own planner archetype with a mostly linear spine and few shortcuts.
- [ ] **Play-test the quest site end to end.** Caravan travel to the site, entering and leaving, quest expiry, and whether the site map should persist after a visit.
- [ ] **Quest-site tuning.** Site map size, loot scaling by difficulty, quest frequency balance, rewards or an asker.
- [ ] **Hostile casket tuning.** Hostile cryosleep occupants may be too strong or weak early; scale by depth and by party size.
- [ ] **Starting supplies.** Check the scavenger start (food, medicine, bedrolls, tools) once a real New Game has been played.
- [ ] **Visible entrances.** Check the marble thresholds and bright lamps on big special rooms in play; consider wide doorways or vestibules.
- [ ] **Fog gates for C-shaped rooms.** RimWorld unfogs a whole connected room at once, so the creep-around reveal needs a custom fog gate (a passable thing that blocks sight) at the bend. Needs an experiment.
- [ ] **Security and mechs.** Re-enable with a threat budget (`Site44Tuning.SecurityEnabled`), dormant units, and a disable-by-terminal option.
- [ ] **Health and recovery.** Care stations (fast med beds) and clearance-linked healing; permadeath ends the game only when the whole party is dead.
- [ ] **Hidden doors and secret objectives** for the later, harder variant.
- [ ] **Tighten the "buildings stay separate" test** in `FacilityGraphTests` (the bound is too loose to fail).
- [ ] **Translation keys** for the terminal and director text (currently English strings in code).

## Done

- [x] Whole-site building plan generator (campus of buildings, stepped footprints, hallways, filled courtyards, isolated buildings) with tests.
- [x] Zone graph over the plan with gates, exterior-door handling and solvability simulation, with tests.
- [x] Furniture, dust, preserved rooms, hermetic crates, cryosleep caskets with survivors, hostiles and corpses.
- [x] Mod settings window (Options > Mod settings > Site-44) and tuning knobs wired to it.
- [x] Ruin quest: site part, linked generation step, opportunity quest script; debug actions and the -site44quest flag to force it.
- [x] Starting gear by skill, outdoor paths, open layout switch, F8/F9 debug hotkeys.

## Design notes: quest-site delivery

- Vanilla pattern (Core, no DLC): a `SitePartDef` plus a `GenStepDef` with `linkWithSite`, and a `QuestScriptDef` named like `OpportunitySite_ItemStash` (autoAccept, `rootSelectionWeight`). Our generator step would run for site maps carrying the Site-44 site part.
- The generator needs a second mode: scale to the site map size, no fixed party start (caravans arrive at the map edge), loot scaled by settings and difficulty.
- The scenario start stays as the "play inside the ruins" option.

## Planned settings

Loot amount, crate density, casket density, survivor casket count, hostile casket share, stray corpses, preserved-room chance, dust amount, furniture ruin level, open versus gated layout, locked-vault share, security on/off and strength, hot zones on/off, quest frequency, and a site-map-size override.


## Morale tuning (to balance in play)
- Mission focus levels, focus tab frequency, delver gear frequency and starting kit are all settings; tune after play.
- Starting kit and the "ready to delve" lift only apply in the Site-44 Breach scenario, not in the -quicktest scenario.
- Ideas: more delver gear (boots for speed, a lamp), a comfort item for rec rooms, a milder darkness penalty.

## Later
- Daily or seeded challenge: the same complex and loadout for everyone, scored on speed or loot. Revisit once the average seeded experience reaches a quality bar.
- Survival layer: before building it, check whether an existing mod already covers the outside-world side so this mod can hook into it instead.

## Principles
- No required expansions or mods, no Harmony patches, no overriding player choices. Anything that changes another system is an off-by-default setting with a tooltip.
- Document known interactions (Ideology demands, caravan events) in the README and in scenario text.
- Possible opt-in setting: force the Classic ideoligion in the Delve scenario, only if play shows people keep tripping over it.

## Pacing and engagement (2026-10)

- Done: room beat planner (`RoomBeats`), headline loot in every beat crate, hostile creature nests, supply setting for medicine and meals, hatch nodes, rare node bonus actions, Delve and Breach default to Small.
- Next: story breadcrumbs as a system of their own (terminal logs, notes, environmental scenes), hazard rooms that need protective gear, a visible per-level goal (restore power, data nodes), set-piece rooms, node Puzzle beat.
- To watch in play: crate density with beats on, nest danger by depth, whether Small is the right default size, whether 25% supply is right outside Delve.
- Rats and other non-insect critters need a hostile faction to behave as nests; only insects for now.
