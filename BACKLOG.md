# Site-44 backlog

Status legend: `[ ]` open, `[~]` in progress, `[x]` done.

## Open

- [ ] **Delver-in-residence mode (design decisions).** Opt-in scenario where the player lives in an enclave town instead of founding a base; no rent for now. Lodging is a ladder earned through standing (cot, room, apartment, suite); furniture allowed inside your own lodging; the player may still build anywhere outside. NPC villagers build the town over time from a plan generated on the fly: staged spawning first (frames later), planned lots revealed as the town decides, growth driven by the player's standing; the town never builds on cells with player buildings, blueprints, frames, zones or home-area marks (plus a margin), re-validates when a project starts, and the player can claim lots. Endgame: claim and restore a cleared complex.
- [ ] **Enclave towns and the meta questline.** A friendly NPC town as a world tile type for delvers: a custom faction whose settlements the world generator places, with a settlement map built by our generator (a compact walled compound with residents and a trader), trading and goodwill from vanilla settlement support, and the town as the quest giver for the ruin sites. A questline then chains sites (medical center, research complex, ...) with data cores as clues. Start with a visitable enclave site with a trader and residents, then upgrade to a full faction settlement.
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


## Settled complex (decided direction)
- One map: the enclave camp starts at the complex's front door and the complex around it is the same map.
- Growth is driven mainly by the player's standing and by sectors the player has cleared. In rare cases the town grows on its own: residents stumble on a valuable room, or take part in an event such as a raid.
- Half-settled look: claimed rooms are cleaned and refurbished in stages; unclaimed ones stay dusty, unmaintained and unexplored. The camp cannot afford to maintain them.
- Keep the generic town generator usable by itself, and provide a way for existing settlements from other mods to know about and interact with Site-44.
- Open: whether the player must be present when an organic expansion or raid happens (see conversation).

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
