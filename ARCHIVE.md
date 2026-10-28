# Archive

Work set aside while Delve goes to beta (see BETA.md). Nothing here is deleted; the design thinking is kept so it can be picked up later. The code for the enclave is preserved in git under the tag `archive/enclave-2026-10`.

## Enclave and town

- **Delver-in-residence mode.** Opt-in scenario where the player lives in an enclave town instead of founding a base; no rent for now. Lodging is a ladder earned through standing (cot, room, apartment, suite); furniture allowed inside your own lodging; the player may still build anywhere outside. NPC villagers build the town over time from a plan generated on the fly: staged spawning first (frames later), planned lots revealed as the town decides, growth driven by the player's standing; the town never builds on cells with player buildings, blueprints, frames, zones or home-area marks (plus a margin), re-validates when a project starts, and the player can claim lots. Endgame: claim and restore a cleared complex.
- **Enclave towns and the meta questline.** A friendly NPC town as a world tile type for delvers: a custom faction whose settlements the world generator places, with a settlement map built by our generator (a compact walled compound with residents and a trader), trading and goodwill from vanilla settlement support, and the town as the quest giver for the ruin sites. A questline then chains sites (medical center, research complex, ...) with data cores as clues.

## Settled complex (decided direction)

- One map: the enclave camp starts at the complex's front door and the complex around it is the same map.
- Growth is driven mainly by the player's standing and by sectors the player has cleared. In rare cases the town grows on its own: residents stumble on a valuable room, or take part in an event such as a raid.
- Half-settled look: claimed rooms are cleaned and refurbished in stages; unclaimed ones stay dusty, unmaintained and unexplored.
- Keep the generic town generator usable by itself, and provide a way for existing settlements from other mods to know about and interact with Site-44.

## Travel between the enclave and complexes

- Realistic (an ox or horse cart takes the party there, possibly for a fee, using the caravan system) or instant (a warp), or both as a setting. Needs enclave services and standing first.

## Roadmap phases archived

- Phase 2: the enclave (site type, residents, services, standing, visitable quest).
- Phase 3: the living town (town plan generator, staged builders, lots, growth, lodging ladder, "Delver in Residence" scenario).
- Phase 4: the wider world (data cores and clues, questline, danger ratings, claim and restore).
- Phase 6: the world layer (expedition contracts, survival layer).

## Known loose ends in the archived work

- Relocated enclave projects are lost on load.
- Villagers do not yet walk to build sites; guards and daily routines are unfinished; deliveries, ranks and a standing readout are unfinished.
