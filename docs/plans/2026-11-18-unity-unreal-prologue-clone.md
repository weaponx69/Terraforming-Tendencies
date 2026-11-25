# Unity Unreal Prologue Clone Implementation Plan

> **For agentic workers:** implement this plan task-by-task, in order, checking off each step as it lands. Steps use checkbox (`- [ ]`) syntax for tracking. Dispatch a fresh subagent per task where the tasks are independent (see dispatching-parallel-agents), otherwise execute inline with a review checkpoint after each task.

**Goal:** Replace Colony Acts as Unity’s default run with an Unreal-faithful Prologue: End Week, O2/beds/power, drone find-ice, guided unlocks, dust storm — flat hex.

**Architecture:** Pure C# colony sim (`PrologueColony`) ports Unreal `FTTColony` Prologue rules; a thin `PrologueController` MonoBehaviour wires hex board, cards, HUD, and End Week. Colony Acts is disabled at boot, not deleted. Construction for v1 uses Unreal build-weeks without Steel/Concrete (documented delta).

**Tech Stack:** Unity C#, NUnit EditMode tests under `Assets/Scripts/Tests/`, existing hex/`ColonyTileGrid`/`PlayerInput`, UGUI/TMP HUD, live Editor via `unity command eval` only (no second Editor / `unity test`).

**Spec:** [`docs/specs/2026-11-18-unity-unreal-prologue-clone-design.md`](../specs/2026-11-18-unity-unreal-prologue-clone-design.md)  
**Unreal source:** `~/Documents/Unreal Projects/TerraformTendencies/Source/TerraformTendencies/TT/`

---

## File map

| File | Responsibility |
| --- | --- |
| `Assets/Scripts/Prologue/PrologueData.cs` | Constants (O2, shuttle, storm, harvest range, building defs) |
| `Assets/Scripts/Prologue/PrologueBuildingId.cs` | Enum / string ids: aquifer, oxygen, solar, habitat, wind_turbine |
| `Assets/Scripts/Prologue/PrologueColony.cs` | Pure sim: week tick, O2, beds, power, sites, prologue step, place preview |
| `Assets/Scripts/Prologue/PrologueBoardState.cs` | Tile cells: feature, deposit left, revealed, building id, site |
| `Assets/Scripts/Prologue/PrologueController.cs` | Scene singleton: start prologue, sync visuals, End Week, card unlocks |
| `Assets/Scripts/Prologue/PrologueDroneScan.cs` | Paint-path reveal from lit ground (step 0 / 4) |
| `Assets/Scripts/UI/PrologueHUD.cs` | Objective, Coming up, AIR, End Week, week report |
| `Assets/Scripts/Tests/PrologueColonyTests.cs` | EditMode tests for sim (no Play Mode required) |
| `PROJECT_KNOWLEDGE.md` | §0 rewritten: Prologue is run model; Colony Acts archived |
| Modify: `ColonyActManager.cs` / scene boot | Do not `BeginRun` when Prologue mode is default |
| Modify: `CardDeckController.cs` | Prologue hand = unlocked set only |
| Modify: `PlayerInput.cs` | Placement reasons from `PrologueColony.Preview` |

**Documented rule delta (v1):** No Steel/Concrete stockpile or builder pool. Build weeks = Unreal table weeks; sites complete after N End Weeks. Full goods/builders port is a follow-on task after Prologue ships.

---

### Task 1: Prologue data + board types

**Files:**
- Create: `Assets/Scripts/Prologue/PrologueBuildingId.cs`
- Create: `Assets/Scripts/Prologue/PrologueData.cs`
- Create: `Assets/Scripts/Prologue/PrologueBoardState.cs`
- Test: `Assets/Scripts/Tests/PrologueColonyTests.cs` (scaffold)

- [ ] **Step 1: Write failing test for constants / building lookup**

```csharp
// Assets/Scripts/Tests/PrologueColonyTests.cs
using NUnit.Framework;
using GameDevTV.RTS.Prologue;

namespace GameDevTV.RTS.Tests
{
    public class PrologueColonyTests
    {
        [Test]
        public void BuildingInfo_HabitatHasTenBedsAndOneBuildWeek()
        {
            var info = PrologueData.Get(PrologueBuildingId.Habitat);
            Assert.AreEqual(10, info.Beds);
            Assert.AreEqual(1, info.BuildWeeks);
        }

        [Test]
        public void LandingOxygen_IsFifty()
        {
            Assert.AreEqual(50, PrologueData.LandingOxygen);
        }
    }
}
```

- [ ] **Step 2: Confirm test fails to compile / fail** (types missing)

Expected: compile error `PrologueData` / `PrologueBuildingId` not found.

- [ ] **Step 3: Implement data types**

```csharp
// Assets/Scripts/Prologue/PrologueBuildingId.cs
namespace GameDevTV.RTS.Prologue
{
    public enum PrologueBuildingId
    {
        None = 0,
        Aquifer,
        Oxygen,
        Solar,
        Habitat,
        WindTurbine,
        CommandPost // landing pad, pre-placed
    }
}
```

```csharp
// Assets/Scripts/Prologue/PrologueData.cs
namespace GameDevTV.RTS.Prologue
{
    public enum PrologueFeature
    {
        None = 0,
        Ice,
        Glacier,
        Minerals // unused in Prologue slice but reserved
    }

    public readonly struct PrologueBuildingInfo
    {
        public readonly PrologueBuildingId Id;
        public readonly string DisplayName;
        public readonly int BuildWeeks;
        public readonly int Beds;
        public readonly int PowerOutput; // solar=1, wind=2 (Unreal wind stronger in hills later; use 1 for both if needed)
        public readonly bool NeedsPower;
        public readonly int IceHarvestPerWeek; // aquifer
        public readonly int IceConsumedPerWeek; // oxygen
        public readonly int OxygenProducedPerWeek; // oxygen when powered + has ice

        public PrologueBuildingInfo(
            PrologueBuildingId id, string name, int buildWeeks, int beds,
            int powerOutput, bool needsPower, int iceHarvest, int iceIn, int o2Out)
        {
            Id = id; DisplayName = name; BuildWeeks = buildWeeks; Beds = beds;
            PowerOutput = powerOutput; NeedsPower = needsPower;
            IceHarvestPerWeek = iceHarvest; IceConsumedPerWeek = iceIn; OxygenProducedPerWeek = o2Out;
        }
    }

    public static class PrologueData
    {
        // From Unreal TTBuildings.h / TTColony.h / TTStory
        public const int LandingOxygen = 50;
        public const double O2PerColonist = 0.5;
        public const int StartColonists = 12;
        public const int HarvestRange = 3; // tiles
        public const int PrologueSteps = 6;
        public const int PrologueShuttleWeeks = 3;
        public const int PrologueShuttleSize = 6;
        public const int PrologueStormWeeks = 2;
        public const int OxygenStoreTargetMult = 3; // step 5: store 3 * O2Need

        public static PrologueBuildingInfo Get(PrologueBuildingId id) => id switch
        {
            PrologueBuildingId.Aquifer => new(id, "Ice Aquifer", 2, 0, 0, false, 8, 0, 0),
            PrologueBuildingId.Oxygen => new(id, "Oxygen Processor", 1, 0, 0, true, 0, 4, 8),
            PrologueBuildingId.Solar => new(id, "Solar Array", 1, 0, 1, false, 0, 0, 0),
            PrologueBuildingId.Habitat => new(id, "Habitat Domes", 1, 10, 0, false, 0, 0, 0),
            PrologueBuildingId.WindTurbine => new(id, "Wind Turbine", 1, 0, 2, false, 0, 0, 0),
            PrologueBuildingId.CommandPost => new(id, "Command Post", 0, 0, 0, false, 0, 0, 0),
            _ => new(PrologueBuildingId.None, "", 0, 0, 0, false, 0, 0, 0)
        };
    }
}
```

```csharp
// Assets/Scripts/Prologue/PrologueBoardState.cs
using System.Collections.Generic;

namespace GameDevTV.RTS.Prologue
{
    public sealed class PrologueTile
    {
        public int Index;
        public PrologueFeature Feature;
        public int DepositLeft; // ice amount remaining
        public bool Revealed;
        public bool Lit; // reachable / power-lit ground for drone start
        public PrologueBuildingId Building;
        public bool Broken;
    }

    public sealed class PrologueSite
    {
        public int Tile;
        public PrologueBuildingId Id;
        public int WeeksDone;
        public int WeeksTotal;
    }

    public sealed class PrologueBoardState
    {
        public readonly List<PrologueTile> Tiles = new();
        public readonly List<PrologueSite> Sites = new();
        public readonly HashSet<int> FirstIceTiles = new();

        public PrologueTile At(int i) => Tiles[i];

        public int NeighborDistance(int a, int b)
        {
            // Filled by controller using ColonyTileGrid / hex distance.
            // For pure tests, inject via PrologueColony.SetDistanceFn.
            return 0;
        }
    }
}
```

- [ ] **Step 4: Run EditMode tests in the connected Editor**

```bash
unity status --format json
# Prefer a project CliCommand if one exists for EditMode tests; otherwise compile-check:
unity command eval "return UnityEditor.EditorUtility.scriptCompilationFailed;" --json
```

If a registered test runner command exists (`unity command` / `unity list`), run only `PrologueColonyTests`.  
**Do not** run `unity test` (spawns second Editor / OOM).

Expected: tests pass; `scriptCompilationFailed` is false.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Prologue Assets/Scripts/Tests/PrologueColonyTests.cs
git commit -m "$(cat <<'EOF'
feat(prologue): add Unreal-aligned building data and board types

EOF
)"
```

---

### Task 2: Pure sim — O2, beds, End Week production

**Files:**
- Create: `Assets/Scripts/Prologue/PrologueColony.cs`
- Modify: `Assets/Scripts/Tests/PrologueColonyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void O2Need_StartColonists_IsSix()
{
    var c = PrologueColony.CreateLanding();
    // 12 colonists * 0.5 = 6 (no claimed-bed relief in Prologue)
    Assert.AreEqual(6, c.O2Need());
}

[Test]
public void EndWeek_PoweredOxygen_AddsNetOxygen()
{
    var c = PrologueColony.CreateLanding();
    // Fixture: ice tile 1, aquifer on 2 (dist 1), oxygen on 3, solar on 4 touching 3
    c.DebugPlaceComplete(2, PrologueBuildingId.Aquifer);
    c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
    c.DebugPlaceComplete(4, PrologueBuildingId.Solar);
    c.SetDistanceFn((a, b) =>
    {
        if ((a == 2 && b == 1) || (a == 1 && b == 2)) return 1;
        if ((a == 3 && b == 4) || (a == 4 && b == 3)) return 1;
        return 99;
    });
    c.Board.At(1).Feature = PrologueFeature.Ice;
    c.Board.At(1).DepositLeft = 100;
    c.Board.At(1).Revealed = true;
    int before = c.Oxygen;
    c.EndWeek();
    // Aquifer +8 ice stock; Oxygen consumes 4 ice produces +8 O2; colonists breathe 6 → net +2 O2 from processor path
    Assert.Greater(c.Oxygen, before - 6); // at least processor ran
}

[Test]
public void TotalBeds_TwoHabitats_IsTwenty()
{
    var c = PrologueColony.CreateLanding();
    c.DebugPlaceComplete(5, PrologueBuildingId.Habitat);
    c.DebugPlaceComplete(6, PrologueBuildingId.Habitat);
    Assert.AreEqual(20, c.TotalBeds());
}
```

- [ ] **Step 2: Run tests — expect fail**

- [ ] **Step 3: Implement `PrologueColony` (core)**

```csharp
// Assets/Scripts/Prologue/PrologueColony.cs
using System;
using System.Collections.Generic;

namespace GameDevTV.RTS.Prologue
{
    public sealed class PrologueColony
    {
        public PrologueBoardState Board { get; } = new();
        public int Week { get; private set; }
        public int Oxygen { get; private set; } = PrologueData.LandingOxygen;
        public int IceStock { get; private set; }
        public int Colonists { get; private set; } = PrologueData.StartColonists;
        public int PrologueStep { get; private set; }
        public bool DustStormActive { get; private set; }
        public bool SolarDarkened => DustStormActive;
        public int ShuttleLandWeek { get; private set; } = -1;
        public bool ShuttleLanded { get; private set; }
        public int StormLandWeek { get; private set; } = -1;
        public HashSet<PrologueBuildingId> Unlocked { get; } = new();

        Func<int, int, int> _dist = (a, b) => a == b ? 0 : 99;

        public static PrologueColony CreateLanding()
        {
            var c = new PrologueColony();
            // Ensure a small board for tests
            for (int i = 0; i < 16; i++)
                c.Board.Tiles.Add(new PrologueTile { Index = i, Lit = i == 0, Revealed = i == 0 });
            c.Board.At(0).Building = PrologueBuildingId.CommandPost;
            c.Board.At(0).Lit = true;
            c.Board.At(0).Revealed = true;
            return c;
        }

        public void SetDistanceFn(Func<int, int, int> fn) => _dist = fn;

        public int Dist(int a, int b) => _dist(a, b);

        public void DebugPlaceComplete(int tile, PrologueBuildingId id)
        {
            Board.At(tile).Building = id;
            Board.At(tile).Revealed = true;
        }

        public int O2Need()
        {
            if (Colonists <= 0) return 0;
            return (int)Math.Ceiling(Colonists * PrologueData.O2PerColonist);
        }

        public double O2Runway()
        {
            int need = O2Need();
            return need <= 0 ? 1e9 : (double)Oxygen / need;
        }

        public int TotalBeds()
        {
            int beds = 0;
            foreach (var t in Board.Tiles)
            {
                if (t.Building == PrologueBuildingId.None || t.Broken) continue;
                beds += PrologueData.Get(t.Building).Beds;
            }
            return beds;
        }

        public bool IsPowered(int tile)
        {
            var b = Board.At(tile).Building;
            if (b == PrologueBuildingId.None) return false;
            var info = PrologueData.Get(b);
            if (!info.NeedsPower) return true;
            if (SolarDarkened)
            {
                // Only wind counts during storm
                foreach (var t in Board.Tiles)
                {
                    if (t.Building != PrologueBuildingId.WindTurbine || t.Broken) continue;
                    if (Dist(t.Index, tile) <= 1) return true;
                }
                return false;
            }
            foreach (var t in Board.Tiles)
            {
                if (t.Broken) continue;
                if (t.Building != PrologueBuildingId.Solar && t.Building != PrologueBuildingId.WindTurbine) continue;
                if (Dist(t.Index, tile) <= 1) return true;
            }
            return false;
        }

        public bool AquiferReachesIce(int aquiferTile)
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Dist(aquiferTile, t.Index) <= PrologueData.HarvestRange) return true;
            }
            return false;
        }

        public List<string> EndWeek()
        {
            var log = new List<string>();
            // 1) Advance sites
            for (int i = Board.Sites.Count - 1; i >= 0; i--)
            {
                var s = Board.Sites[i];
                s.WeeksDone++;
                if (s.WeeksDone >= s.WeeksTotal)
                {
                    Board.At(s.Tile).Building = s.Id;
                    log.Add($"Completed {PrologueData.Get(s.Id).DisplayName}");
                    Board.Sites.RemoveAt(i);
                }
            }

            // 2) Harvest ice from aquifers
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Aquifer || t.Broken) continue;
                int gained = 0;
                foreach (var ice in Board.Tiles)
                {
                    if (!ice.Revealed || ice.DepositLeft <= 0) continue;
                    if (ice.Feature != PrologueFeature.Ice && ice.Feature != PrologueFeature.Glacier) continue;
                    if (Dist(t.Index, ice.Index) > PrologueData.HarvestRange) continue;
                    int take = Math.Min(PrologueData.Get(PrologueBuildingId.Aquifer).IceHarvestPerWeek - gained, ice.DepositLeft);
                    ice.DepositLeft -= take;
                    gained += take;
                    if (gained >= PrologueData.Get(PrologueBuildingId.Aquifer).IceHarvestPerWeek) break;
                }
                IceStock += gained;
            }

            // 3) Oxygen processors
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                if (!IsPowered(t.Index)) { log.Add("Oxygen Processor unpowered"); continue; }
                var info = PrologueData.Get(PrologueBuildingId.Oxygen);
                if (IceStock < info.IceConsumedPerWeek) { log.Add("Oxygen Processor starved for ice"); continue; }
                IceStock -= info.IceConsumedPerWeek;
                Oxygen += info.OxygenProducedPerWeek;
            }

            // 4) Breathe
            int need = O2Need();
            int breathed = Math.Min(need, Oxygen);
            Oxygen -= breathed;
            if (breathed < need) log.Add("AIR CRITICAL: not enough oxygen");

            // 5) Shuttle
            if (!ShuttleLanded && ShuttleLandWeek >= 0 && Week + 1 >= ShuttleLandWeek)
            {
                ShuttleLanded = true;
                Colonists += PrologueData.PrologueShuttleSize;
                log.Add($"{PrologueData.PrologueShuttleSize} colonists landed");
            }

            // 6) Storm start
            if (StormLandWeek >= 0 && Week + 1 >= StormLandWeek)
                DustStormActive = true;

            Week++;
            StoryCheck(log);
            return log;
        }

        public void StoryCheck(List<string> log)
        {
            // Step advancement — see Task 3 for full rules; keep stub that only unlocks nothing yet
        }
    }
}
```

- [ ] **Step 4: Tests pass**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Prologue/PrologueColony.cs Assets/Scripts/Tests/PrologueColonyTests.cs
git commit -m "$(cat <<'EOF'
feat(prologue): End Week O2, beds, and power rules

EOF
)"
```

---

### Task 3: Placement preview + Prologue step machine

**Files:**
- Modify: `Assets/Scripts/Prologue/PrologueColony.cs`
- Modify: `Assets/Scripts/Tests/PrologueColonyTests.cs`

- [ ] **Step 1: Failing tests for preview + steps**

```csharp
[Test]
public void Preview_AquiferTooFarFromIce_Refuses()
{
    var c = PrologueColony.CreateLanding();
    c.Unlocked.Add(PrologueBuildingId.Aquifer);
    c.Board.At(1).Feature = PrologueFeature.Ice;
    c.Board.At(1).DepositLeft = 50;
    c.Board.At(1).Revealed = true;
    c.SetDistanceFn((a, b) => (a == 10 && b == 1) || (a == 1 && b == 10) ? 4 : 99);
    Assert.IsFalse(c.Preview(10, PrologueBuildingId.Aquifer, out string reason));
    StringAssert.Contains("3", reason);
}

[Test]
public void Preview_SolarNotTouchingOxygen_RefusesWhenPlacingSolarForPowerCheck()
{
    // Placing oxygen is allowed without power; IsPowered false until solar touches.
    var c = PrologueColony.CreateLanding();
    c.Unlocked.Add(PrologueBuildingId.Oxygen);
    c.Unlocked.Add(PrologueBuildingId.Solar);
    Assert.IsTrue(c.Preview(3, PrologueBuildingId.Oxygen, out _));
}

[Test]
public void StoryCheck_AfterIceRevealed_AdvancesToStep1AndUnlocksAquifer()
{
    var c = PrologueColony.CreateLanding();
    c.StartPrologue();
    Assert.AreEqual(0, c.PrologueStep);
    c.Board.At(2).Feature = PrologueFeature.Ice;
    c.Board.At(2).DepositLeft = 40;
    c.Board.At(2).Revealed = true;
    c.Board.FirstIceTiles.Add(2);
    c.StoryCheck(new System.Collections.Generic.List<string>());
    Assert.AreEqual(1, c.PrologueStep);
    Assert.IsTrue(c.Unlocked.Contains(PrologueBuildingId.Aquifer));
}
```

- [ ] **Step 2: Expect fail**

- [ ] **Step 3: Implement `Preview`, `TryPlace`, `StartPrologue`, full `StoryCheck`**

Rules (from Unreal):

```
Preview order:
1. tile occupied by building or site → refuse
2. id not in Unlocked → "Not yet: finish the objective at the top first"
3. aquifer: must be within HarvestRange of revealed ice with DepositLeft > 0
4. otherwise OK (oxygen/solar/habitat/wind have no feature lock in Prologue)

TryPlace: enqueue PrologueSite with BuildWeeks from PrologueData

StartPrologue:
  PrologueStep = 0
  Unlocked clear
  ShuttleLandWeek = -1
  StormLandWeek = -1

StoryCheck advancement (after place / reveal / EndWeek):
  step 0 → 1 when any ice revealed (record FirstIceTiles)
  step 1 → 2 when aquifer complete reaching that ice; unlock oxygen+solar
  step 2 → 3 when powered oxygen exists; set ShuttleLandWeek = Week + PrologueShuttleWeeks; unlock habitat
  step 3 → 4 when TotalBeds >= 20 (2 habitats) OR (beds >= colonists after shuttle — Unreal asks for 2 habitats)
  step 4 → 5 when second aquifer reaches ice NOT in FirstIceTiles (or first deposit dry AND new ice drilled);
           schedule StormLandWeek = Week + PrologueStormWeeks; unlock wind
  step 5 → 6 (done) when DustStormActive was true and then storm weeks elapsed
            AND Oxygen >= OxygenStoreTargetMult * O2Need at storm arrival (ride-out: Oxygen > 0 each week)

ObjectiveText(): port strings from FTTColony::PrologueObjective default switch
```

Implement `ObjectiveText(out PrologueBuildingId suggestBuild, out bool wantDrone)` with the Unreal copy from the design spec.

- [ ] **Step 4: Tests pass**

- [ ] **Step 5: Commit**

```bash
git commit -am "$(cat <<'EOF'
feat(prologue): placement preview and guided step unlocks

EOF
)"
```

---

### Task 4: Disable Colony Acts; boot PrologueController

**Files:**
- Create: `Assets/Scripts/Prologue/PrologueController.cs`
- Modify: `Assets/Scripts/Player/ColonyActManager.cs` (guard `BeginRun`)
- Modify: `PROJECT_KNOWLEDGE.md` §0 header

- [ ] **Step 1: Add compile-time / runtime switch**

```csharp
// In ColonyActManager.BeginRun / HandlePlanetGenerated — at top:
if (PrologueController.IsActive)
{
    Debug.Log("[ColonyActManager] Archived: Prologue mode owns the run.");
    return;
}
```

```csharp
// Assets/Scripts/Prologue/PrologueController.cs
using UnityEngine;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Prologue
{
    public class PrologueController : MonoBehaviour
    {
        public static PrologueController Instance { get; private set; }
        public static bool IsActive => Instance != null && Instance.enabled;

        public PrologueColony Colony { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            Colony = PrologueColony.CreateLanding();
            // Replace CreateLanding board with real hex indices when PlanetGenerator finishes
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            if (PlanetGenerator.Instance != null)
                HandlePlanetGenerated();
        }

        void OnDestroy()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            if (Instance == this) Instance = null;
        }

        void HandlePlanetGenerated()
        {
            SyncBoardFromPlanet();
            Colony.StartPrologue();
        }

        public void SyncBoardFromPlanet()
        {
            // Map PlanetGenerator / HexGridManager tiles → PrologueBoardState
            // Mark home pad Lit+Revealed; hide ice until drone reveals
            // Wire SetDistanceFn to ColonyTileGrid hex distance
        }

        public void EndWeek()
        {
            var log = Colony.EndWeek();
            PrologueHUD.Instance?.ShowWeekReport(log);
            PrologueHUD.Instance?.Refresh(Colony);
            RefreshHandUnlocks();
        }

        void RefreshHandUnlocks()
        {
            // Tell CardDeckController which PrologueBuildingIds are unlocked
        }
    }
}
```

- [ ] **Step 2: Scene wiring**

Via connected Editor:

```bash
unity command eval "var go = new UnityEngine.GameObject(\"PrologueController\"); go.AddComponent<GameDevTV.RTS.Prologue.PrologueController>(); return go.name;" --json
```

Persist into the main play scene (save scene after adding). Disable or remove auto-start UI for BetweenActShop / Acts panel from default visible state.

- [ ] **Step 3: Update PROJECT_KNOWLEDGE.md**

Replace §0 one-sentence game and owner table: PrologueColony / PrologueController are authoritative; Colony Acts marked **Archived**. Point to the design spec.

- [ ] **Step 4: Live smoke**

```bash
unity command eval "UnityEditor.EditorApplication.isPlaying = true;" --json
# wait, then:
unity command eval "return GameDevTV.RTS.Prologue.PrologueController.IsActive;" --json
unity command eval "return GameDevTV.RTS.Player.ColonyActManager.Instance != null && GameDevTV.RTS.Player.ColonyActManager.Instance.ActIndex;" --json
```

Expected: Prologue active; Acts did not begin a Materials quota run (or ActIndex stays unset / BeginRun skipped).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Prologue/PrologueController.cs Assets/Scripts/Player/ColonyActManager.cs PROJECT_KNOWLEDGE.md
git commit -m "$(cat <<'EOF'
feat(prologue): boot Prologue as default; archive Colony Acts run

EOF
)"
```

---

### Task 5: Card hand unlocks + placement bridge

**Files:**
- Modify: `Assets/Scripts/Player/CardDeckController.cs`
- Modify: `Assets/Scripts/Player/PlayerInput.cs`
- Create or remap card assets for: Ice Aquifer, Oxygen Processor, Solar Array, Habitat Domes, Wind Turbine

- [ ] **Step 1: Map cards to `PrologueBuildingId`**

Add helper:

```csharp
public static bool TryGetPrologueId(BlueprintCardSO card, out PrologueBuildingId id)
{
    id = PrologueBuildingId.None;
    if (card == null) return false;
    string n = card.cardName ?? card.name;
    if (n.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0
        || n.IndexOf("Ice", System.StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("Drill", System.StringComparison.OrdinalIgnoreCase) >= 0)
    { id = PrologueBuildingId.Aquifer; return true; }
    if (n.IndexOf("Oxygen", System.StringComparison.OrdinalIgnoreCase) >= 0) { id = PrologueBuildingId.Oxygen; return true; }
    if (n.IndexOf("Solar", System.StringComparison.OrdinalIgnoreCase) >= 0) { id = PrologueBuildingId.Solar; return true; }
    if (n.IndexOf("Habitat", System.StringComparison.OrdinalIgnoreCase) >= 0) { id = PrologueBuildingId.Habitat; return true; }
    if (n.IndexOf("Wind", System.StringComparison.OrdinalIgnoreCase) >= 0) { id = PrologueBuildingId.WindTurbine; return true; }
    return false;
}
```

When `PrologueController.IsActive`, hand rebuild = only unlocked prologue cards (plus drone/scan card for step 0/4). Strip Acts seating (climate force-seat, CP handoff, Materials filters).

- [ ] **Step 2: PlayerInput ghost uses `Colony.Preview`**

On place confirm: `Colony.TryPlace(tileIndex, id)` then spawn construction ghost / complete mesh via existing building prefabs if available; else colored plinth.

- [ ] **Step 3: Manual place path smoke in Play Mode** (eval + play)

Confirm locked card cannot place; unlocked aquifer refuses far from ice.

- [ ] **Step 4: Commit**

```bash
git commit -am "$(cat <<'EOF'
feat(prologue): lock hand to Unreal unlocks and preview placement

EOF
)"
```

---

### Task 6: Drone scan reveal

**Files:**
- Create: `Assets/Scripts/Prologue/PrologueDroneScan.cs`
- Hook input for paint path

- [ ] **Step 1: Behavior**

- Player enters scan mode when objective `wantDrone`  
- Path must start from a `Lit` revealed tile  
- Each tile flown over becomes `Revealed`  
- If ice/glacier found: add to board, if step 0 record `FirstIceTiles`, call `StoryCheck`  
- Lit frontier expands along revealed tiles adjacent to buildings/power (keep simple: all revealed tiles become Lit)

- [ ] **Step 2: Test pure reveal helper**

```csharp
[Test]
public void RevealPath_MarksIceAndAdvancesStory()
{
    var c = PrologueColony.CreateLanding();
    c.StartPrologue();
    c.Board.At(3).Feature = PrologueFeature.Ice;
    c.Board.At(3).DepositLeft = 30;
    c.RevealTiles(new[] { 1, 2, 3 });
    Assert.IsTrue(c.Board.At(3).Revealed);
    Assert.AreEqual(1, c.PrologueStep);
}
```

Implement `RevealTiles(IEnumerable<int> path)` on `PrologueColony`.

- [ ] **Step 3: Wire UI / click path in controller**

- [ ] **Step 4: Commit**

```bash
git commit -am "$(cat <<'EOF'
feat(prologue): drone paint-path ice discovery

EOF
)"
```

---

### Task 7: Prologue HUD + End Week

**Files:**
- Create: `Assets/Scripts/UI/PrologueHUD.cs`
- Hide Acts weeks/quota panels while Prologue active

- [ ] **Step 1: HUD fields**

- Objective text (`Colony.ObjectiveText`)  
- Coming up: shuttle weeks / storm weeks  
- AIR: `Oxygen`, `Δ` estimate, `O2Runway` weeks  
- End Week button → `PrologueController.EndWeek`  
- Week report toast (reuse or simple TMP)

- [ ] **Step 2: Refresh each place / week / reveal**

- [ ] **Step 3: Live Play Mode: complete scripted soft path with eval cheats if needed**

Add temporary `PrologueController.DebugSkipToStep(int)` for trailer setup only (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`).

- [ ] **Step 4: Commit**

```bash
git commit -am "$(cat <<'EOF'
feat(prologue): HUD objective, AIR meter, and End Week

EOF
)"
```

---

### Task 8: Dust storm + Prologue complete + fellowship smoke

**Files:**
- Modify: `PrologueColony.cs` storm duration / clear  
- Optional: `Assets/Scripts/Prologue/PrologueBot.cs` for autoplay smoke

- [ ] **Step 1: Storm rules**

When `StormLandWeek` reached: `DustStormActive = true` for `PrologueStormWeeks` weeks. Solar does not power; Wind does. After storm ends and step 5 conditions met → `PrologueStep = 6`, fire `OnPrologueComplete`.

- [ ] **Step 2: EditMode test**

```csharp
[Test]
public void DustStorm_DarkensSolar_WindKeepsOxygenPowered()
{
    var c = PrologueColony.CreateLanding();
    c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
    c.DebugPlaceComplete(4, PrologueBuildingId.Solar);
    c.DebugPlaceComplete(5, PrologueBuildingId.WindTurbine);
    c.SetDistanceFn((a, b) => Math.Abs(a - b) <= 1 ? Math.Abs(a - b) : 99);
    Assert.IsTrue(c.IsPowered(3));
    c.DebugForceStorm(true);
    Assert.IsTrue(c.IsPowered(3)); // wind touching
    // Remove wind
    c.Board.At(5).Building = PrologueBuildingId.None;
    Assert.IsFalse(c.IsPowered(3));
}
```

- [ ] **Step 3: Full Play Mode walkthrough** (human or bot): steps 0→5 without Acts UI  

Checklist:

- [ ] No Materials quota / Terra-Coin shop  
- [ ] Objective advances  
- [ ] Storm darkens solar  
- [ ] Survive with O2 > 0  

- [ ] **Step 4: Commit**

```bash
git commit -am "$(cat <<'EOF'
feat(prologue): dust storm pressure and Prologue completion

EOF
)"
```

---

### Task 9: Docs + archive note for fellowship

**Files:**
- Modify: `PROJECT_KNOWLEDGE.md` (finish §0 rewrite)  
- Optional: `GDD.md` pointer only if lore conflicts  

- [ ] **Step 1: Document archived Colony Acts**, Prologue owner scripts, steel/concrete delta  
- [ ] **Step 2: List follow-on** Industry port (do not implement)  
- [ ] **Step 3: Commit**

```bash
git commit -am "$(cat <<'EOF'
docs: Prologue run model replaces Colony Acts in project knowledge

EOF
)"
```

---

## Spec coverage (self-review)

| Spec requirement | Task |
| --- | --- |
| Replace Colony Acts default | 4, 9 |
| Prologue steps 0–5 + unlocks | 3, 5, 6 |
| End Week | 2, 7 |
| O2 / beds / power | 2, 8 |
| Drone find ice | 6 |
| Placement within 3 / solar touch | 3, 5 |
| Dust storm | 8 |
| HUD objective + AIR + End Week | 7 |
| Flat hex, no planetoid | all (reuse hex) |
| No adjacency combos | 3–5 (Preview has no combo score) |
| Fellowship playable slice | 8 smoke |
| Steel/Concrete deferred | File map delta note |

**Placeholder scan:** none intentional; Storm clear timing uses Unreal `PrologueStormWeeks`.  
**Type names:** `PrologueBuildingId`, `PrologueColony`, `PrologueController`, `PrologueHUD` consistent across tasks.

---

## Execution notes for agents

1. Never `unity test` / `unity build` / second Editor — use connected `unity command eval` + EditMode compilation.  
2. Commit after each task when the user has asked for commits, or batch commits if the user prefers fewer; default in this plan assumes commits are allowed during execution (confirm with owner if unsure).  
3. Prefer matching Unreal numbers over inventing balance.  
4. Do not reintroduce Colony Acts Materials quotas into the Prologue HUD.  
