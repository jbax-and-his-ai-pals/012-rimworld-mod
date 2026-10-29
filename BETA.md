# Delve beta plan

Goal: a first beta-quality build of Site-44 that a stranger can install, start, and enjoy without knowing anything about its development. Delve is the headline. Nothing half-built is visible. The enclave and town work is archived (see ARCHIVE.md).

## What "beta" means here

- A player starts the Delve scenario and understands what to do within the first minutes, without reading the README.
- A run, from the first level to the end screen, is stable, readable and fair, at every difficulty preset.
- A published mod's baseline polish: clean log, saves and loads, a proper description and preview, no debug leftovers.
- Everyone can use it: accessibility is a core principle, not a final pass.

## Quality bars (all must hold before release)

**Stability**
- A real New Game (not test flags) plays 10 levels deep, ends by party death, and shows the end screen, with no errors in the log.
- Saving and loading mid-level and mid-run works, including keycards, badges, nests and the level chain.
- Level generation time is acceptable at every size, and no combination of settings blocks progress.

**Accessibility**
- No colour-only cues; every state also has a shape or a word.
- Secondary text is at the normal font size and readable contrast; nothing essential lives only in a hover tooltip.
- Plain wording, no jargon, no talking down. Every window (terminal puzzle, biome page, letters, gate controls, settings) gets a pass.
- Everything scales with the UI scale setting.

**Onboarding**
- The scenario description and a first-level message explain the goal: find the extraction terminal, reach the gate, what carries down.
- Each level shows what is left to do.

**Balance**
- The first five levels are learnable at Default; Easier is forgiving; Harder and above are optional.
- Medicine, infection, nests and the level ramp are tuned from play, not guesses.

**Release hygiene**
- About.xml, version, README, Workshop text, preview image, compatibility notes (Ideology, Biotech, Odyssey).
- Debug hotkeys and test flags are off or clearly dev-only. Strings are moved into keyed files for translation.

## Scope

**Must (blocks the beta)**
1. End-to-end test of the real scenario path, plus a save and load test.
2. A visible per-level goal readout.
3. End-of-run summary: depth reached, best depth, what killed the party.
4. Two simple choices per new game, difficulty and pace (done), with everything else under Custom settings; each game keeps its own copy of the settings (done).
5. A threat budget per level, spent on creature nests and security sectors (done; tuning from play remains).
6. First-time messages that explain the gate, terminals, keycards and clearance.
7. The accessibility pass over every window.
8. A balance pass over the first ten levels with the presets.
9. Release hygiene above.
10. Archive the enclave code out of the shipped build, with a git tag preserving it.

**Should (makes it feel complete)**
- Story breadcrumbs as one small system: terminal logs and notes that tell a short arc across a run.
- Set-piece rooms and a guardian room at depth milestones.
- Hazard rooms that need the right gear (cold gear exists; heat is the missing half).
- The between-levels stop (D.5), if the descent options in settings do not already cover it.
- An arcade-style score so runs can be compared.

**Could (if time allows)**
- A seeded or daily run, hidden doors and secret objectives, non-insect creature nests, more complex types.

**Not in the beta**
- Everything in ARCHIVE.md. Breach and quest sites keep working, are not promoted, and receive no new work.

## Proposed milestones

1. **Measure.** Real-scenario test, save and load test, log clean, headline bugs fixed.
2. **Guide.** Onboarding messages, per-level goal, end summary, settings and window accessibility pass.
3. **Depth.** Security decision, story breadcrumbs, set pieces, hazard rooms.
4. **Tune.** Balance across presets and the first ten levels, from play.
5. **Ship.** Release hygiene, archive the enclave code, publish.

## Open decisions

- Whether Breach and quest sites ship in the beta at all, or sit behind a setting.
- How much story: a few terminal logs, or one authored arc per run.
- Strip the enclave code now, or after the beta build is stable.
