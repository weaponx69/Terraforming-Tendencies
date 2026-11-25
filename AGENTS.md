# Agent Instructions — Terraforming Tendencies

## Read before any task (in order)

1. **[`.cursor/rules/vertical-slice-not-module.mdc`](.cursor/rules/vertical-slice-not-module.mdc)** — Prologue = **vertical slice of the full game**, not a separate module; no “prologue-only” silos for core systems.
2. **[`PROJECT_KNOWLEDGE.md`](PROJECT_KNOWLEDGE.md)** — authoritative architecture and current systems (start with **§0**).
3. **[`GDD.md`](GDD.md)** — when the change affects rules, balance, or player experience.

Spec mirror: [`docs/specs/2026-11-24-vertical-slice-authority.md`](docs/specs/2026-11-24-vertical-slice-authority.md).

## Additional rules (when relevant)

| Topic | Document |
|-------|----------|
| **Vertical slice = the game (permanent)** | [`.cursor/rules/vertical-slice-not-module.mdc`](.cursor/rules/vertical-slice-not-module.mdc) + [`docs/specs/2026-11-24-vertical-slice-authority.md`](docs/specs/2026-11-24-vertical-slice-authority.md) |
| **Player verification (whole project — no smoke tests)** | [`docs/specs/2026-11-23-player-verification-policy.md`](docs/specs/2026-11-23-player-verification-policy.md) + [`.cursor/rules/player-verify-after-changes.mdc`](.cursor/rules/player-verify-after-changes.mdc) |
| **Prologue UI playthrough bot (resume / automate slice)** | [`PROJECT_KNOWLEDGE.md`](PROJECT_KNOWLEDGE.md) **§10.1** — harnesses, env vars, recent code touchpoints, open work |
| Unity CLI / live Editor commands (**CLI only — Unity MCP deprecated**) | [`.zoo/rules/UnityCLI-Automation.md`](.zoo/rules/UnityCLI-Automation.md) |
| Visual Scripting conversion | [`.zoo/rules/UnityVisualScripting-conversion.md`](.zoo/rules/UnityVisualScripting-conversion.md) |
| GitHub Copilot | [`.github/copilot-instructions.md`](.github/copilot-instructions.md) |
| Zoo custom modes | [`.zoomodes`](.zoomodes) |

If `PROJECT_KNOWLEDGE.md` and `plans/project_knowledge.md` disagree, follow **`PROJECT_KNOWLEDGE.md`**.

## Visible files

Write every file the owner should be able to open in a visible folder. Design docs go in `docs/specs/`. Plans go in `docs/plans/`.

Never create, move, or edit a file whose name starts with `.`, or whose path contains a folder that starts with `.`. That includes `.summer/`, `.zoo/`, `.github/`, and `.cursor/`. If an existing note already lives in one of those folders, leave it there and put the new file in a visible folder.
