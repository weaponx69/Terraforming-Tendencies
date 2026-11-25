# Prologue playtest automation — session handoff (2026-11-23)

**Agents:** Read [`PROJECT_KNOWLEDGE.md`](../../PROJECT_KNOWLEDGE.md) **§10.1** first (canonical). This file is the longer narrative from the Nov 23 automation push.

## User goals (chronological)

1. Performance/RAM and texture streaming (handled earlier in branch).
2. **Prologue UI playthrough bot** — survey through full prologue win; loop until complete.
3. **Unity CLI** for automation — avoid step-by-step desktop-only instructions ([`.zoo/rules/UnityCLI-Automation.md`](../../.zoo/rules/UnityCLI-Automation.md)).
4. **X Chat self-notify** when the bot needs help (`@HvnlyGamesBoss`); Cloud Agent had X auth; some **local Agent threads** stayed `needsAuth` until a **new chat** / Reload Window.
5. **FPS + RAM monitoring**; abort if Editor RSS exceeds limit (default 12 GiB via `PROLOGUE_RAM_LIMIT_MB=12288`).
6. **No hidden folders** — playtest outputs under **`Assets/Playtest/`**, not `Assets/.summer/playtest/`.

## Verification policy

[`docs/specs/2026-11-23-player-verification-policy.md`](../specs/2026-11-23-player-verification-policy.md) + [`.cursor/rules/player-verify-after-changes.mdc`](../../.cursor/rules/player-verify-after-changes.mdc). The Prologue slice **is** the product loop for sign-off.

## Operational notes

- Concurrent runs blocked by **flock** on `/tmp/prologue-ui-playthrough.lock`.
- Runaway loops stopped with `pkill -f prologue-ui-playthrough-until-win`.
- Multiple **Game** views + “no cameras rendering” — [`tools/unity-editor-reset.sh`](../../tools/unity-editor-reset.sh).
- Recommended: `SKIP_ENTER_PLAY=1 SKIP_RECOMPILE=1 ./tools/prologue-ui-playthrough-until-win.sh` with Editor already playing the slice.
- Primary log: `/tmp/prologue-until-win.log`; help inbox: `Assets/Playtest/player-help-inbox.txt`.

## Code changes (summary)

See §10.1 table. Placement and Prologue command gating were the main gameplay-facing fixes; bot harness lives in `PrologueUiPlaythrough` + shell scripts.

## Remaining

1. Wire X Chat in `notify-player-help.sh`.
2. Until-win exit 0 through step 6 without getting stuck on Habitat/faith/contract cancel.
3. Optional: repo-visible logs under `Assets/Playtest/logs/`.

## First message in a fresh Agent chat

```text
Read PROJECT_KNOWLEDGE §10.1. Verify X (get_users_me). Continue until-win prologue UI playthrough via Unity CLI.
```