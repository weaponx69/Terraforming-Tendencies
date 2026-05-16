# Project Overview
- Game Title: Terriforming Tendencies (from project root path)
- High-Level Concept: An RTS or simulation game involving planet configuration and environment setup (based on `PlanetConfig` and `AutoHookup` script).
- Players: Single player (implied).
- Render Pipeline: Universal Render Pipeline (URP).
- Target Platform: StandaloneLinux64.

# Game Mechanics
## Core Gameplay Loop
- Configuring planets and environmental assets (rocks/boulders).
- Managing RTS elements (based on `GameDevTV.RTS` namespace in scripts).

# UI
- Standard Unity Editor UI.
- AI Assistant integration (failing).
- Version Control (PlasticSCM) integration (reporting mismatch).

# Key Asset & Context
- `Library/`: Contains the databases (`ArtifactDB`, `SourceAssetDB`) reporting the "readonly" error.
- `Assets/Scripts/Editor/AutoHookup.cs`: Script that triggers `AssetDatabase.SaveAssets()` on load, likely surfacing the database issue.
- `com.unity.ai.assistant`: Package spawning background tasks that result in the `waitpid` error.

# Implementation Steps
## Step 1: Resolve Database Lock & Permissions
The "attempt to write a readonly database" error is the most likely root cause. It prevents Unity from tracking external tasks correctly.
1. **Close Unity Editor**: Ensure no ghost processes are running (`Unity`, `UnityHub`, `upm`).
2. **Clear Temporary Files**: Delete the `Library`, `Temp`, and `Logs` folders in the project root. This forces Unity to recreate the databases and release any file locks.
3. **Verify Disk Space**: Check if the drive containing the project has sufficient free space. SQLite (Unity's DB engine) will report a readonly error if it cannot create journal files due to lack of space.
4. **Verify Permissions**: Ensure the user `brian` has full read/write permissions for `/home/brian/UnityProjects/Terriforming Tendencies`.

## Step 2: Address AI Assistant & Version Control
1. **Re-link Cloud Project**: The `MismatchingRepositoryProjectMessage` suggests the local project isn't correctly aligned with the Unity Cloud project. Open **Project Settings > Services** and ensure the Project ID is correct.
2. **Refresh AI Assistant**: Once the databases are healthy, the AI Assistant should be able to initialize its local task state without triggering the `waitpid` error.

# Verification & Testing
1. **Restart Unity**: Open the project again.
2. **Check Console**: Ensure the `attempt to write a readonly database` error is gone.
3. **Trigger AutoHookup**: Ensure the "Successfully found and hooked up..." log appears without subsequent errors.
4. **Monitor Background Tasks**: Check the task progress bar in the bottom right of the editor to ensure "Package Manager" or "Burst" tasks complete without `waitpid` errors.
