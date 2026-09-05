# AI Agent Instructions: Unity CLI Automation

**Authoritative CLI help:** run `unity --help` / `unity <command> --help` for the installed version.
This project uses the experimental Unity CLI plus the Unity Pipeline package (`com.unity.pipeline`) to talk to a **already-running** Editor.

## Hard rules for this project

1. **Unity MCP is deprecated for this project. Do not use it.**
   - Do not call Cursor/`user-Unity` MCP tools, `mcp_auth` for Unity, or `unity mcp configure` as part of agent workflows.
   - Live Editor automation is **Unity CLI only**: `unity status`, `unity command`, `unity list`, `unity command eval`.
2. **Prefer the live connected Editor.** Check with:
   ```bash
   unity status --format json
   ```
3. **Do NOT spawn a second Editor** for tests or inspection while the user's Editor is open.
   - Forbidden here: `unity test`, `unity build`, `unity run`, and legacy `-batchmode` / `-quit` headless Editor launches.
   - This project is extremely heavy (large hex grid). A second Editor will OOM-kill both processes on Linux.
4. **Use `unity command` / `unity list` / `unity command eval` against the connected Editor** for live inspection, Play Mode control, and Editor-side tools.

## Connected-Editor workflow (default for agents)

### Status and discovery
```bash
unity status --format json
unity command --format json          # list commands the connected Editor exposes
unity list --format json             # same family: registered Pipeline tools + schemas
```

### Live C# eval (REPL)
Fast, no domain reload required for simple queries. Prefer `--json` for parseable output.
```bash
unity command eval "return Application.version;" --json
unity command eval "return UnityEditor.EditorApplication.isPlaying;" --json
unity command eval "return GameObject.Find(\"Camera Target\").transform.position;" --json
```

### Run a registered Editor command
```bash
unity command <command-name> --arg Value --json
```
Custom project commands may be exposed via `[CliCommand]`. Discover them with `unity command` / `unity list`.

### Bugfix / verification loop
When iterating on a gameplay bug with the live Editor:
1. Confirm `unity status` shows this project as `ready`.
2. `unity command eval "UnityEditor.EditorApplication.isPlaying = true;"` to enter Play Mode.
3. Inspect state with `unity command eval ... --json`.
4. Edit C# in the repo; let the Editor recompile.
5. Restart Play Mode via `eval` and re-verify.
6. Do not stop until live `eval` evidence confirms the fix.

## Safe CLI uses that do NOT open a second Editor

These are fine even with the Editor open:

| Command | Use |
|---------|-----|
| `unity status` | Connected Editor health |
| `unity command` / `unity list` | Live Editor tools |
| `unity pipeline` / `unity pipe` | Pipeline package install/upgrade/inspect |
| `unity doctors` / `unity doctor` | CLI environment diagnostics |
| `unity logs` | CLI/Hub logs |
| `unity editors -i` | List installed Editors |
| `unity auth status` | Login state |
| `unity upgrade` | Self-update the CLI binary |

## Unsafe / deprecated for agents

| Command / path | Why |
|----------------|-----|
| Unity MCP / `user-Unity` / `unity mcp` | **Deprecated** — do not use for agent automation |
| `unity test` | Spawns Editor / test runner; OOM risk |
| `unity build` | Batch-mode Editor spawn |
| `unity run` | Batch-mode Editor / headless run |
| `unity open` | May launch another Editor instance |

If automated tests are required and no Editor is open, ask the user first. Prefer verifying behavior through the live Editor + `unity command eval`.

## Output formats (automation)

```bash
unity status --format json
unity command eval "return 1;" --json
```

- Interactive default: `human`
- Piped default: `tsv`
- Prefer `--format json` / `--json` for agents
- Errors go to **stderr**; exit codes are differentiated (`0` ok, `2` usage, `6` command failure, etc.)

## Project path

Commands that need a project usually auto-detect. If not:
```bash
export UNITY_PROJECT_PATH="/home/brian/UnityProjects/Terraforming Tendencies"
# or
unity command eval "return 1;" --project-path "/home/brian/UnityProjects/Terraforming Tendencies"
```

## Current expectation

Before using live commands, the user should have this project open in Unity with Pipeline connected. If `unity status` shows nothing `ready`, tell the user to open the project in the Editor rather than launching a second instance.
