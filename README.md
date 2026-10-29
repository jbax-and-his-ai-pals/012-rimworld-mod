# 012-rimworld-mod

**Site-44**: a RimWorld 1.6 mod that adds procedurally generated, explorable ruined complexes: office campuses, hospitals, barracks bases, research compounds, industrial plants and residential blocks. Each one is a believable abandoned building, with real hallways, room programs, furniture, dust and decay, loot, sleepers in cryosleep caskets, locked vaults and the occasional droid.

It needs only the base game (no DLC) and adds no Harmony patches.

> RimWorld is a trademark of Ludeon Studios. This is an unofficial fan project and is not affiliated with or endorsed by Ludeon Studios.

## What it does

- **Whole-building generation.** A planner lays out one to several buildings per site: bars, combs, rings, stepped and chamfered footprints, atria, suites, interlocking rooms, link corridors and fire doors, with a start yard in front of a main entrance. Another layer carves the plan into sectors along its real doors.
- **Six kinds of complex**, each with its own building mix, room programs, outdoor props, loot tables and flavor:
  office complex, medical center, barracks base, research complex, industrial plant, residential block.
- **Five sizes**: extra small, small, medium, large and XL (the whole map).
- **Offered as quest sites.** Each type is a world-map site offered by an opportunity quest, so complexes turn up in an ordinary game. A scenario, *Site-44 Breach*, starts you inside one instead.
- **Things to find.** Hermetic loot crates, cryosleep caskets (a lone survivor who joins you, a hostile sleeper, a body, or just loot), dried-out corpses, rec rooms, kitchens, dormitories, clinics, libraries, workshops, armories, comms rooms, classrooms, preserved rooms with better loot, and secure rooms behind terminal-locked doors holding a treasure vault, a prototype droid or a volatile cache.
- **A safe start.** Nothing hostile is placed near where you arrive, and stray hostiles from other map generation steps are removed.
- **Terminals and security.** Gate and diagnostic terminals open a cipher-lock puzzle (pick the access code from look-alikes; skill gives more attempts and fewer decoys). Security consoles let you seize or shut down a group of turrets and droids.
- **Delve mode.** The *Site-44 Delve* scenario chains complexes as levels: hack the extraction terminal, step through the gate with your party and the loot stacked beside it, and arrive somewhere a little bigger and harder. Losing the whole party ends the run.
- **A friendly enclave.** A settlement of residents who grow their town as your standing with them rises.
- **Mission focus and delver kit.** A gentle mood lift inside complexes, a non-addictive focus tab, delver gear, and a starting kit so a run does not begin with chores.
- **Settings.** Options, Mod settings, Site-44: loot amount, crate and casket density, survivors, hostile share, dust, furniture damage, outdoor props, room variety, secure rooms, rec rooms, layout openness, complex type and size, quest frequency.

## Compatibility and expansions

The mod aims to sit quietly beside everything else:

- No expansion is required, nothing is patched with Harmony, and no player choice is overridden. Anything that would change another system is a setting, off by default, and explained in its tooltip.
- **Ideology:** the game asks for an ideoligion on every new game, and its precepts can demand altars, rituals or buildings that a delving party cannot provide (mood penalties and alerts such as "altar desired"). If you play the Delve or Breach scenarios with Ideology, choosing the **Classic** ideoligion on that page avoids nearly all of these.
- **Delve rules:** the Delve scenario is about the ruins, not an outside world, so when a run starts it switches off groups of random events: enemy raids, caravan events, other threats (infestations, manhunters), visitors and outside arrivals, and weather disasters. Each group is a switch under Options, Mod settings, Site-44, Delve rules, and the choice is saved with the run. Only the Delve scenario is affected; other scenarios are never touched.
- **New game setup (Delve):** the Delve scenarios use nothing from the world except the start tile's climate and biome, so the first page asks about difficulty, pace and focus, and a *More setup* row decides how much else you set up. *Just play* starts with sensible defaults (Cassandra as the storyteller, a default world and start tile, a random biome from the ordinary set). *Customize Site-44 settings* adds a page of per-game choices and the biome list. *Full RimWorld setup* uses the game's own pages for storyteller, difficulty, world and landing site, and can be combined with the customize page. Breach keeps the ordinary world pages, and none of this is used with the Odyssey expansion.
- **Why Full RimWorld setup exists, and when to use it:** Just play and Customize replace RimWorld's world and landing-site pages with an automatic step, which means any other mod that adds its own page to new-game setup, or replaces the world-creation screen, never gets to show it on those paths. Mods that work on world generation itself or on the colonist page still run as usual, because we use the game's normal code for those. If you rely on a mod that adds pages to setup (world tech level, custom world builders and similar), or you simply want to pick a storyteller, RimWorld's difficulty settings or an exact landing site, tick *Full RimWorld setup*; it hands the whole setup back to the game so that every mod sees the flow it expects.
- **Focus tabs** are a real drug and appear in drug policies (added to the social drugs policy by default).
- Quick-test launches (developer flags below) use the vanilla crash-landing scenario, so scenario-only behavior such as the starting kit needs a real new game.
- If another mod misbehaves with this one, please open an issue with the load order and the debug log.

**Quick-test scenario:** *Site-44 Delve (quick test)* uses tiny complexes with the extraction gate beside the start and already open, to try the level chain fast. The launch flag `-site44quickdelve` does the same in developer runs.

## Install

1. Copy (or junction) the `Site44` folder into your RimWorld `Mods` folder.
2. Enable **Site-44** in the mod list.

To build from source you need the .NET 8 SDK and RimWorld installed; the project references the game's own DLLs.

```powershell
cd Site44
dotnet build -c Release      # writes Assemblies\Site44.dll
```

If RimWorld is not in `C:\steam\steamapps\common\RimWorld`, pass `-p:RimWorldDir=<path>`.

## Tests

The generators are pure C# (no game references) and are covered by unit tests.

```powershell
dotnet test Site44.Tests
```

## Developer tools

Debug menu entries appear under **Site-44** when dev mode is on. Hotkeys (rebindable): **F7** hero mode on selected colonists (full clearance, power armor, near-invincible), **F8** unseal every locked sector, **F9** toggle fog drawing.

Launch flags: `-site44test` (with `-quicktest`, builds the complex on the quick-start map), `-site44quest[=all|Medical|...]` (forces ruin quests and generates their maps), `-site44size=XS|S|M|L|XL`, `-site44type=<Type>`, `-site44hero`.

## Layout of the repository

| Path | Contents |
|---|---|
| `Site44/` | the mod: `About`, `Defs`, `Patches`, `Source` |
| `Site44/Source/Gen` | pure generation logic: building planner, sector graph, interior layouts |
| `Site44/Source/GenSteps` | the map generation step, decoration, props, containers |
| `Site44.Tests/` | unit tests for the pure logic |
| `ROADMAP.md`, `BACKLOG.md` | where the project is going |

## Status

Work in progress. See [ROADMAP.md](ROADMAP.md).

## License

MIT, see [LICENSE](LICENSE).
