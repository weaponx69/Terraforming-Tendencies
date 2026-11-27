# (Canonical copy in Cursor rule + PK §10.1)

**Binding guardrails (never reintroduce traps):** [`.cursor/rules/rts-perf-budget.mdc`](../../.cursor/rules/rts-perf-budget.mdc).

**Implemented mitigations & probe scripts:** [`PROJECT_KNOWLEDGE.md`](../../PROJECT_KNOWLEDGE.md) — search `MainGamePerformanceBudget`, `main-game-fps-probe.sh`.

Historical hot-spot table removed from this stub to avoid drift; see git history if needed.

## Quick verify

```bash
SKIP_ENTER_PLAY=1 ./tools/click-order-diagnostic.sh
# maxDispatchSpikeMs < 2.0 in Assets/Playtest/click-order-diagnostic-report.json
```

```bash
SKIP_ENTER_PLAY=1 SKIP_RECOMPILE=1 ./tools/main-game-ui-playthrough.sh
# inspect OUT_DIR steps.jsonl "fps" field
```