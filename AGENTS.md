# Agent instructions — Terraforming Tendencies

**Start here.** Cursor already loads [`.cursor/rules/`](.cursor/rules/) (`alwaysApply`), including [`vertical-slice-not-module.mdc`](.cursor/rules/vertical-slice-not-module.mdc): the main-scene run is **the game**, not a separate module—do not fork or “prologue-only” core mechanics without explicit direction.

**Wording:** In chat with the owner, frame tasks as **game / main run / vertical slice** work—not “prologue work.” Only say “prologue” when naming a file, type, or design step (0–6), and clarify that those names are legacy labels on the shipped loop. Do **not** read [`PROJECT_KNOWLEDGE.md`](PROJECT_KNOWLEDGE.md) cover to cover.

**Out of scope:** Summer Engine MCP/skills, Hostinger, and other global Cursor skills unless the workspace is explicitly that product.

## Authority

| Topic | Wins |
|--------|------|
| Mechanics, architecture, harnesses | [`PROJECT_KNOWLEDGE.md`](PROJECT_KNOWLEDGE.md) |
| Lore / tone / design intent | [`GDD.md`](GDD.md) (not run rules) |

If `PROJECT_KNOWLEDGE.md` and `plans/project_knowledge.md` disagree, follow **PROJECT_KNOWLEDGE.md**.

## Default read (most tasks)

1. **§0** — vertical slice run model + owner script table  
2. **§2** — code map / where logic lives  

Optional: **§4** (don't reintroduce), **§10** (Unity CLI), **§10.1** (UI bot).

Balance or narrative changes: add **GDD.md**.

## Task router

| You're working on… | Also use | Verify (if player-visible) |
|--------------------|----------|----------------------------|
| Slice gameplay, UI, survey, cards, perf | Rules: `vertical-slice`, `player-verify`, `rts-perf-budget`, `survey-team-no-camera-fly` (Survey Team card only) | [`tools/prologue-ui-playthrough.sh`](tools/prologue-ui-playthrough.sh) or [`tools/click-order-diagnostic.sh`](tools/click-order-diagnostic.sh) for perf/input |
| Unity Editor / CLI / eval | PK **§10**, rule `unity-cli.mdc` | `unity status` → `ready` |
| Feature design (colonists, DEM, research…) | Matching `docs/specs/2026-11-*.md` only | Same as row 1 if it ships in slice |
| Archived Colony Acts | PK **§3**, §0.2–0.7 (reference) | [`tools/colony-acts-player-bot-cli.sh`](tools/colony-acts-player-bot-cli.sh) only if asked |
| Docs-only / comments / pure refactor | — | Build if C# touched; no Play required |

**Player verify (summary):** One Editor; real UI; evidence (screenshot, `steps.jsonl`, or observed outcome). Compile-only ≠ done. Full policy: [`docs/specs/2026-11-23-player-verification-policy.md`](docs/specs/2026-11-23-player-verification-policy.md). Harness detail: PK **§10.1**.

## Visible files for new work

Design → `docs/specs/`. Plans → `docs/plans/`. Do not add **new** owner-facing docs under dot-prefixed folders (`.summer/`, `.zoo/`). **Exception:** maintain existing [`.cursor/rules/`](.cursor/rules/) for agents.