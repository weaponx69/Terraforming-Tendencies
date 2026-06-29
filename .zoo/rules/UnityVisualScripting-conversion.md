# Unity Visual Scripting Conversion Guidelines
- Never convert heavy mathematical algorithms or procedural loops directly into Flow Graphs. 
- If a C# script is a "Core System," refactor it to include `[IncludeInSettings]` and `[Inspectable]` attributes instead of deleting it.
- When generating visual state transitions, prioritize Unity State Graphs over complex boolean Flow loops.
- Avoid introducing script compilation errors. Ensure all public namespaces remain intact.
- CRITICAL: Never delete or empty Visual Scripting graph assets (.asset ScriptGraphAsset files) and replace their logic with C# code. VS graphs must always be fixed in-place by correcting their internal variables, node connections, or scope settings — never replaced or gutted.
- CRITICAL: When a VS graph has a runtime error (e.g., "Variable not found"), the solution is to fix the graph's variable declarations or node configurations, NOT to port the logic to C# or remove the ScriptMachine.

