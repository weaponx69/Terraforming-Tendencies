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

### 3. Running Automated Tests
The CLI provides structured output that is easy to parse.
* To run tests and get a clean JSON summary, run:
  `unity test run --projectPath . --testPlatform PlayMode --format json`
* Always read the JSON output (or stderr) to find stack traces and analyze failures.

### Iteration Loop Instructions
When instructed to fix a bug via a `/goal` or autonomous loop:
1. Use `unity command eval` to inspect the live state of the game if needed.
2. Edit the C# files to apply a fix.
3. Use the Unity CLI to run the automated tests (`unity test run ...`).
4. Read the JSON output to determine if tests passed. 
5. If tests fail, analyze the stack trace, edit the code, and repeat. Do not stop iterating until all tests pass or you are fundamentally blocked.
