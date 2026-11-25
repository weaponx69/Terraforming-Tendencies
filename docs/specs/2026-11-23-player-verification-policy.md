# Player verification policy (entire project)

**Rule:** A feature is not verified until it is exercised **in Play Mode the way a player would** — real UI, real input, real game state. **No smoke tests** may count as “done,” “passing,” or “shipped.”

## Vertical slice = the game (for now)

The scene everyone plays today is labeled **Prologue**, but architecturally it is the **vertical slice** that will grow into the full title:

- Shared planet: [`PlanetGenerator`](../../Assets/Scripts/Environment/PlanetGenerator.cs), deposits, DEM grounding  
- Shared player stack: [`PlayerInput`](../../Assets/Scripts/Player/PlayerInput.cs), cards, [`ColonyTileGrid`](../../Assets/Scripts/Player/ColonyTileGrid.cs), buildings, HUD  
- Run owner: [`PrologueController`](../../Assets/Scripts/Prologue/PrologueController.cs) + [`PrologueColony`](../../Assets/Scripts/Prologue/PrologueColony.cs) (slice sim — not a disposable tutorial silo)

Treat **Prologue*** naming as **temporary packaging**, not permission to build “prologue-only” rules that will never apply to sector 2, multiplayer, or post-contract play. When you verify the slice (UI playthrough, bootstrap, survey, mines), you are verifying **the product loop**, not an optional module.

**Colony Acts** is archived legacy; do not treat it as an equal second game mode for new design or sign-off.

## What counts as verification

| Requirement | Meaning |
|-------------|---------|
| **Play Mode** | Connected Editor, one instance ([`UnityCLI-Automation`](../../.zoo/rules/UnityCLI-Automation.md)); no headless batch Editor for agent loops. |
| **Player path** | Clicks, keys, cards, End Week, placement ghosts — same surface the player sees. |
| **Evidence** | Screenshot(s), step log, or eval that reflects **outcome after** the action (not “method returned true”). |
| **Fresh world when it matters** | Runs that depend on spawned content must **re-bootstrap** that content (deposit bootstrap on the slice — see below). |

## Project harnesses (use these)

| Area | Harness | Notes |
|------|---------|--------|
| **Vertical slice (current main game)** | [`tools/prologue-ui-playthrough.sh`](../../tools/prologue-ui-playthrough.sh) | End-to-end player UI for the slice; `BeginNewPrologueRunForPlaytest()` every run → board sync + deposit bootstrap + fog. |
| **Archived Acts** | [`tools/colony-acts-player-bot-cli.sh`](../../tools/colony-acts-player-bot-cli.sh) | Legacy bot only if deliberately testing old Acts code — not the product direction. |
| **Single feature in slice** | Manual Play + shortest UI path | Same rules: real UI; document clicks and outcome. |

## Not allowed as verification

- **Compile-only** or “grep proves it” for gameplay.
- **Smoke / golden-path unit tests** that skip UI (`TryPlaceImmediate` chains, fake boards pretending to be a playthrough).
- **Cheat shortcuts** as proof: [`ColonyActsDevDemo`](../../Assets/Scripts/Player/ColonyActsDevDemo.cs), instant-place playthrough runners, mass `eval` that teleports state without UI.
- **`unity test` / second Editor** spawn (OOM risk; not player-shaped anyway).
- Claiming “verified” when Play Mode or Pipeline was unavailable.

## Unit tests (allowed, narrow role)

EditMode tests (e.g. [`PrologueColonyTests`](../../Assets/Scripts/Tests/PrologueColonyTests.cs), movement/nav tests) may assert **rules and math** on small fakes.

They **do not** replace player verification and must not be described as playthroughs or smoke coverage for a feature.

## World bootstrap (required for slice / E2E runs)

Automated **vertical-slice** verifies must run:

1. `PrologueController.BeginNewPrologueRunForPlaytest()` — new colony, `SyncBoardFromPlanet`, [`PrologueSectorDepositBootstrap`](../../Assets/Scripts/Prologue/PrologueSectorDepositBootstrap.cs), ice markers, resource fog.
2. Then [`PrologueUiPlaythrough`](../../Assets/Scripts/Prologue/PrologueUiPlaythrough.cs) + shell clicks **or** equivalent manual UI.

Skipping bootstrap means the run is **invalid** for slice sign-off (deposits/`?` will lie about real player experience). When the full game adds more boot steps, they belong in the same “fresh world” path — not a separate test harness.

## Prologue / vertical slice

Same rules as the rest of the project. Bootstrap for automated runs: `BeginNewPrologueRunForPlaytest()` then UI playthrough (see **World bootstrap** above). Supersedes any older prologue-only verification doc.

## Agents and contributors

Always apply [`.cursor/rules/player-verify-after-changes.mdc`](../../.cursor/rules/player-verify-after-changes.mdc). Entry point: [`AGENTS.md`](../../AGENTS.md). Harness detail: [`PROJECT_KNOWLEDGE.md`](../../PROJECT_KNOWLEDGE.md) §10.1.