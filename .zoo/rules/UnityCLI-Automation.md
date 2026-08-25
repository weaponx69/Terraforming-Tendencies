# AI Agent Instructions: Unity CLI Automation

**IMPORTANT FOR ALL AGENTS (Antigravity, Zoo Code, Roo Code, etc.):**
Whenever you need to run Unity tests, inspect the live scene, or execute C# code without compiling, you MUST use the new Unity CLI (`unity` command) rather than the legacy `-batchmode` headless approach.

## How to use the Unity CLI
The project is already configured with `com.unity.pipeline`. This means you can interact with the Editor in real-time from the terminal!

### 1. Live C# Execution (The REPL)
You can evaluate any C# expression inside the running Editor. This is incredibly fast (milliseconds) and does not require domain reloads. Use `--json` for predictable output.
* Examples:
  * `unity command eval "return Application.version;"`
  * `unity command eval "return UnityEditor.EditorApplication.isPlaying;"`
  * `unity command eval "return GameObject.Find(\"Camera Target\").transform.position;" --json`

### 2. Discovering Custom Commands
The project may expose custom tools via the `[CliCommand]` attribute. 
* To see what operations are available to you, run: `unity command`
* To execute a command: `unity command <command-name> --arg Value`

### 3. Running Automated Tests & Iteration Loop
**CRITICAL RULE: YOU MUST NEVER USE `unity test run` OR `-batchmode`!** 
This project is extremely heavy (14,000+ hex tiles). If you launch a background headless Unity Editor to run tests while the user already has the main Editor open, it will consume all system RAM and the Linux OOM Killer will instantly assassinate both Editors!

You are strictly forbidden from launching background instances. You must ONLY use `unity command eval` to communicate with the single, live Editor.

When instructed to fix a bug via a `/goal` or autonomous loop:
1. Use `unity command eval "UnityEditor.EditorApplication.isPlaying = true;"` to start Play Mode in the user's live Editor.
2. Use `unity command eval` to inspect the live state of the game, read coordinates, or verify fixes.
3. Edit the C# files to apply a fix.
4. Restart Play Mode via `eval` to reload the code, then use `eval` again to verify if your fix worked.
5. Do not stop iterating until you have verified the fix works using `eval`.
