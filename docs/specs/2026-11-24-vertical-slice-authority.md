# Vertical slice authority (game model)

**Status:** Permanent project decision  
**Audience:** Humans and coding agents  

## Decision

The **Prologue** in the main Terraforming Tendencies scene is **not** a standalone tutorial module or temporary content pack. It is the **vertical slice**: the prototype that **becomes the whole game**.

The same run model will widen (more sectors, planet selection, etc.); the slice is not replaced by a different game loop later.

## What that means in code

- Types and paths named `Prologue*` currently **own the authoritative run** (week clock, board, survey, cards, placement, End Week).
- New work should **generalize through those systems**, not fork “full game” duplicates.
- **Colony Acts** is **archived**; it is not the forward path when the vertical slice is active.

## Agent rule

Cursor agents must load [`.cursor/rules/vertical-slice-not-module.mdc`](../.cursor/rules/vertical-slice-not-module.mdc) (`alwaysApply: true`) and treat it as binding alongside [`AGENTS.md`](../AGENTS.md) and [`PROJECT_KNOWLEDGE.md`](../PROJECT_KNOWLEDGE.md) §0.

## Anti-patterns (do not reintroduce)

- “Prologue-only” perf or feature branches for core mechanics  
- Describing the slice as disposable or unrelated to shipping architecture  
- Verification or design that assumes Acts or a future module is the real game  