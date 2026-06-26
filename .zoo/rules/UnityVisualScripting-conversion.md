# Unity Visual Scripting Conversion Guidelines
- Never convert heavy mathematical algorithms or procedural loops directly into Flow Graphs. 
- If a C# script is a "Core System," refactor it to include `[IncludeInSettings]` and `[Inspectable]` attributes instead of deleting it.
- When generating visual state transitions, prioritize Unity State Graphs over complex boolean Flow loops.
- Avoid introducing script compilation errors. Ensure all public namespaces remain intact.

