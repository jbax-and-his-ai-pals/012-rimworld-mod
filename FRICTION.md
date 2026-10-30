# Delve friction audit

Written 2026-10-30, after Harmony became acceptable (see the project notes). It lists the actions a player takes in a Delve run, where each one costs extra clicks, hidden steps or surprises, and the lightest way to fix it.

How to read it:

- **Seen in code** means I read it in this repository. **Vanilla (unverified)** means it is how I believe the base game behaves, and it needs a check against the decompiled game source before anything is built on it.
- **Fix kinds:** *Def* (XML only), *Code* (our own classes, jobs, UI, settings), *Patch* (Harmony), *Decision* (needs the owner's call first).
- Rule of thumb: patch only when a def, a custom job or our own UI cannot do it. Every patch gets a setting and a startup log line.
- Nothing here is built yet.

## 1. Starting a run

| Step | Friction | Fix |
|---|---|---|
| New game | Two choices (difficulty, pace) plus Custom. The Run tab and settings carry the rest. Seen in code. | None needed now. |
| First level | Arrival message is a transient `Messages.Message` ("Depth N: type."), not a letter, so it disappears. Seen in code. Fails the "nothing essential lives only in a tooltip or toast" bar. | Code: a letter for the goal on level 1 and a short one on each new tier. |
| Starting kit | 6 survival meals, 3 focus tabs, 2 medicine, a cap and vest per colonist. Seen in code. Drafted colonists will not eat. | See "Needs" below. |

## 2. Moving and exploring

| Step | Friction | Fix |
|---|---|---|
| Colonists are mostly drafted | Drafted colonists do no work, so anything that normally happens by work priority (tending, eating, sleeping, hauling, looting) does not happen. This is the root of most friction below. | See sections 4 and 5. |
| Sealed hatches and gates | One terminal click per hatch group. Fine. | None. |
| Exposure | The Run tab shows hours until serious harm, but nothing warns when a colonist walks into a harmful room. | Code: an `Alert` (no Harmony) when a colonist is in a room beyond their range. Also helps accessibility. |

## 3. Terminals and gates

| Step | Friction | Fix |
|---|---|---|
| Right-click with several selected | Fixed this session (best skilled, reachable colonist). Seen in code. | Done. |
| Cipher puzzle window | `forcePause` is on, so the game pauses while the puzzle is open. Seen in code. Failure re-opens straight away. | None. |
| Failed hack | May trigger the alarm. The message is a toast. Seen in code. | Optional: make an alarm a letter (it already is for the alarm itself). |
| Diagnostic logs | Opens a pausing dialog. Fine. | None. |
| Descend | One colonist uses the gate; the whole party is carried. Seen in code. Good. | None. |

## 4. Descending

| Step | Friction | Fix |
|---|---|---|
| Items left on the floor | `Descend` moves only the colonists. The `gatePosition` argument is never used, and the old comment about "loot stacked beside the gate" no longer matches the code. The previous map is then removed. Anything not in a colonist's inventory is lost. Seen in code. | Decision: do we want a carry-over stash (see section 6)? Until then, correct the comment. |
| Healing on the way down | `delveDescentCare` (0 nothing, 1 dress wounds, 2 heal, 3 full) and `delveDescentRest` already exist. Seen in code. | None. A "Care" mode for in-level use is the missing half. |
| Drafted state across a descent | The code despawns and respawns each colonist. Whether they stay drafted is not checked. | Verify in play; if undrafted, restore the state. |
| Camera and speed | Camera jumps to the start. Game speed is not changed. | Code: pause on arrival so the player can look before time runs. Setting. |

## 5. Needs and care while drafted

| Step | Friction | Fix |
|---|---|---|
| Hunger | Drafted colonists do not go and eat. Vanilla (unverified). They carry meals, but nothing prompts a meal. | Code: an ordered "eat from inventory" job, offered as a Run tab button; optionally an alert when hunger is low. |
| Rest | Drafted colonists cannot sleep, so tiredness only goes away at the gate if `delveDescentRest` is on. | Code: a "rest" order (lie down in the nearest bed or on the floor). Decision on how rest should work in a delve. |
| Tending | Verified in the decompiled 1.6 source. A drafted doctor can already be told to tend: right-click a wounded colonist, one doctor selected (`FloatMenuOptionProvider_DraftedTend`, which is single-select only). That menu only offers medicine the doctor carries (`onlyUseInventory: true`), otherwise "without medicine". The normal work path (`WorkGiver_Tend`) only tends a standing humanlike patient if they are in a bed, which a drafted party rarely is. | Code, no patch: "Tend everyone" issues the same ordered job (`TendPatient`, `draftedTend = true`) but chooses medicine with `HealthAIUtility.FindBestMedicine` without the inventory-only limit. The tend driver already has toils to fetch medicine from the ground or from another colonist's pack, so the fetch step comes for free. |
| Self-tend | Verified: allowed per colonist by `playerSettings.selfTend`; the drafted menu offers it on the colonist themselves (`CanSelfTarget`). | Decision: when to use it automatically. |
| Joy and mood | `ThoughtWorker_MissionFocus`, corpse hardening and a starting lift already exist. Seen in code. Joy is refilled at start only. | Probably fine. Watch mental breaks in play. |

Where a patch could still matter: only for fully automatic tending (a drafted colonist deciding to tend by themselves when no hostile is near). A mod-side check on a timer that issues the ordered job can do the same without a patch, so that is the first thing to try.

## 6. Inventory and loot

| Step | Friction | Fix |
|---|---|---|
| Crates and containers | Loot sits in containers (`Site44_SupplyCrate` and vanilla crates). Drafted colonists cannot haul. Getting items to the right colonist is manual. | Code: "collect loot in this room" order that distributes by free capacity. |
| Gear for hazards | Wearing the right piece for the next room is manual and each swap takes time. | Code: "dress for the next room" button using the gear catalogue and the exposure numbers. |
| Medicine and consumables | Who carries what is invisible except on each colonist's gear tab. | Code: a Supplies section on the Run tab with quick-use buttons. |
| Carry-over | Only what colonists carry survives a descent. | Decision: a shared party stash that carries over (a game component) with a locker in each entry zone. |

## 7. Alerts and information

| Item | Friction | Fix |
|---|---|---|
| Bleeding or downed colonists | Vanilla alerts exist. | None. |
| Hazard rooms | No alert as a colonist enters a harmful room. | Code: alert (see section 2). |
| Per-level goal | On the Run tab. Needs a visible prompt when something changes (a letter on level 1, then silent). | Code: letter on level 1. |

## 8. Settings and polish

| Item | Friction | Fix |
|---|---|---|
| Settings count | Around 60 fields. More systems mean more toggles. Seen in code. | Decision: keep a short "Recommended" page and move tuning under Custom. |
| Debug hotkeys | `GameComponent_Site44Hotkeys` is still active. Seen in code. | Release hygiene: off by default or dev-only. |
| `About.xml` says "uses no Harmony patches" and BACKLOG.md lists "no Harmony patches" as a principle | Stale after the 2026-10-30 decision. | Update both when the first patch ships. |

## Recommended first batch

1. **Letters and alerts** (no Harmony): first-level goal letter, exposure alert, hunger alert. Small and helps accessibility.
2. **Medic's loadout and "Tend everyone"** as ordered jobs with a fetch step. Tells us whether a patch is needed.
3. **Dress for the next room** and the **Supplies** section.
4. **Shared stash** after a decision.
5. **Auto-tend when safe**, as a timer in a map component that issues the ordered job. Reach for a Harmony patch only if that proves clumsy.

Summary of the audit: none of the first four items needs Harmony. The audit found one real gap (loot on the floor is lost at a descent) and two quick wins (a first-level goal letter, a hazard alert).

Open decisions for the owner: pause on arrival, how rest works in a delve, a carry-over stash, auto-tend behaviour, and the settings page shape.
