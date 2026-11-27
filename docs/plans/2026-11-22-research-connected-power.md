# Research + Connected Power Implementation Plan

> **For agentic workers:** implement this plan task-by-task, in order, checking off each step as it lands. Steps use checkbox (`- [ ]`) syntax for tracking. Dispatch a fresh subagent per task where the tasks are independent (see dispatching-parallel-agents), otherwise execute inline with a review checkpoint after each task.

**Goal:** MainGame uses real connected power (existing pressurized tubes) from Tier‑1, a camp→grid energy pool that Orbital Scan spends, and research cards that cost weeks to unlock higher-tier seating.

**Architecture:** Keep board rules in `MainGameColony` (EditMode-testable): adjacency power graph + energy pool. Drive visuals by ensuring finished MainGame `BaseBuilding`s get `PowerNode` auto-links (existing `ConnectTo` / `PressurizedTube`). Research is a small MainGame state machine (active project + weeks + unlock set) that gates hand seating and lights the pause tech tree; Tier‑1 starts unlocked.

**Tech Stack:** Unity 6000.4, existing `PowerNode` / `PressurizedTube` / `TechTreeSO`, NUnit EditMode tests, Unity CLI on the open Editor.

**Spec:** [`docs/specs/2026-11-22-research-connected-power-design.md`](../specs/2026-11-22-research-connected-power-design.md)

---

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/MainGame/MainGameData.cs` | `CampEnergyGeneration`, `OrbitalScanEnergyCost`, research week costs; remove mineral Orbital cost |
| `Assets/Scripts/MainGame/MainGameColony.cs` | Energy pool, `IsPowered` via adjacency graph, research progress / unlocks |
| `Assets/Scripts/MainGame/MainGameResearch.cs` | (new) Research card target ids, seating flags, week tick helper |
| `Assets/Scripts/Player/ScoutingCardSO.cs` | Orbital spends energy; `CanApply` / `Apply` / play cost display |
| `Assets/Scripts/Player/CardDeckController.cs` | Orbital spend path; seat Research card; gate Tier‑2+ cards |
| `Assets/Scripts/MainGame/MainGameCardMap.cs` | Orbital energy (not minerals) for cost display |
| `Assets/Scripts/UI/MainGameHUD.cs` | Show energy + real power status |
| `Assets/Scripts/MainGame/MainGameConstruction.cs` | On finish: ensure `PowerNode` + `AutoConnect` path fires |
| `Assets/Scripts/UI/PauseMenuUI.cs` / `TechTreeUI.cs` | Show MainGame tech ladder with Tier‑1 lit |
| `Assets/Resources/Cards/OrbitalScanCard.asset` | Description: energy cost |
| `Assets/Resources/Cards/ResearchCard.asset` | (new) Research scouting/blueprint card |
| `Assets/Scripts/Tests/MainGameColonyTests.cs` | Energy, graph power, research weeks |
| `PROJECT_KNOWLEDGE.md` | Document shipped behavior |

---

### Task 1: Energy pool constants + colony API (TDD)

**Files:**
- Modify: `Assets/Scripts/MainGame/MainGameData.cs`
- Modify: `Assets/Scripts/MainGame/MainGameColony.cs`
- Modify: `Assets/Scripts/Tests/MainGameColonyTests.cs`

- [x] **Step 1: Write failing tests**

Add to `MainGameColonyTests.cs`:

```csharp
[Test]
public void Energy_CampFillsPool_OrbitalSpends()
{
    var c = MainGameColony.CreateLanding();
    c.StartMainGame();
    Assert.GreaterOrEqual(c.Energy, MainGameData.OrbitalScanEnergyCost);
    Assert.IsTrue(c.TrySpendEnergy(MainGameData.OrbitalScanEnergyCost));
    int after = c.Energy;
    Assert.IsFalse(c.TrySpendEnergy(c.Energy + 1));
    Assert.AreEqual(after, c.Energy);
}

[Test]
public void Energy_RefillsFromCampOnEndWeek_StormCutsCamp()
{
    var c = MainGameColony.CreateLanding();
    c.StartMainGame();
    while (c.TrySpendEnergy(1)) { }
    Assert.AreEqual(0, c.Energy);
    c.EndWeek();
    Assert.AreEqual(MainGameData.CampEnergyGeneration, c.Energy);
    c.DebugForceStorm(true);
    while (c.TrySpendEnergy(1)) { }
    c.EndWeek();
    Assert.AreEqual(0, c.Energy); // camp dark, no generators
}
```

- [x] **Step 2: Run tests — expect FAIL**

Run EditMode filter `Energy_Camp` via Unity CLI / Test Runner.  
Expected: compile errors or FAIL (`Energy` / `TrySpendEnergy` missing).

- [x] **Step 3: Implement minimal API**

In `MainGameData.cs`, add energy constants (keep `OrbitalScanMineralCost` until Task 2):

```csharp
/// <summary>Camp grid fill per End Week while storm has not darkened camp.</summary>
public const int CampEnergyGeneration = 8;
/// <summary>Orbital Scan spend from the energy pool (not minerals).</summary>
public const int OrbitalScanEnergyCost = 5;
```

`OrbitalScanMineralCost` stays until Task 2 migrates Orbital play path / greps clean.

In `MainGameColony.cs` (near stocks):

```csharp
public int Energy { get; private set; }

public int EnergyGeneration()
{
    int gen = SolarDarkened ? 0 : MainGameData.CampEnergyGeneration;
    foreach (var t in Board.Tiles)
    {
        if (t.Building == MainGameBuildingId.None || t.Broken) continue;
        // Sites still building do not generate.
        bool underConstruction = false;
        foreach (var s in Board.Sites)
            if (s.Tile == t.Index) { underConstruction = true; break; }
        if (underConstruction) continue;
        gen += MainGameData.Get(t.Building).PowerOutput;
    }
    return gen;
}

public bool TrySpendEnergy(int amount)
{
    if (amount <= 0) return true;
    if (Energy < amount) return false;
    Energy -= amount;
    return true;
}

void RefillEnergy()
{
    Energy = EnergyGeneration();
}
```

Call `RefillEnergy()` at end of `CreateLanding` / `StartMainGame`, and in `EndWeek` after storm flags update (match test: refill on EndWeek after drain).

- [x] **Step 4: Re-run tests — expect PASS**

- [x] **Step 5: Commit**

```bash
git add Assets/Scripts/MainGame/MainGameData.cs Assets/Scripts/MainGame/MainGameColony.cs Assets/Scripts/Tests/MainGameColonyTests.cs
git commit -m "$(cat <<'EOF'
feat(prologue): add camp energy pool for Orbital spends

EOF
)"
```

---

### Task 2: Orbital Scan spends energy (not minerals)

**Files:**
- Modify: `Assets/Scripts/Player/ScoutingCardSO.cs`
- Modify: `Assets/Scripts/Player/CardDeckController.cs`
- Modify: `Assets/Scripts/MainGame/MainGameCardMap.cs`
- Modify: `Assets/Resources/Cards/OrbitalScanCard.asset` (description text only)
- Modify: `Assets/Scripts/UI/MainGameHUD.cs` (optional note path already in Apply)

- [x] **Step 1: Update CanApply / GetMaterialsPlayCost / Apply**

In `ScoutingCardSO.cs` MainGame Orbital branches:

```csharp
// CanApply
return colony.Energy >= MainGameData.OrbitalScanEnergyCost;

// GetMaterialsPlayCost — Orbital no longer uses minerals in MainGame
return 0;

// Apply
int cost = MainGameData.OrbitalScanEnergyCost;
if (!colony.TrySpendEnergy(cost)) return;
// ... PingNearestIce ...
MainGameHUD.Instance?.ShowNote(... $"Orbital Scan (−{cost} energy)." ...);
```

In `CardDeckController.cs`, replace any `OrbitalScanMineralCost` / `TrySpendStock(Minerals)` Orbital path with `TrySpendEnergy` / `OrbitalScanEnergyCost`.

In `MainGameCardMap.MineralCost`, Orbital returns `0` (energy is not minerals). Add a helper if UI needs it:

```csharp
public static int EnergyCost(BlueprintCardSO card)
{
    if (card is ScoutingCardSO scout
        && scout.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan)
        return MainGameData.OrbitalScanEnergyCost;
    return 0;
}
```

Update `OrbitalScanCard.asset` description to say energy, not minerals.

- [x] **Step 2: Compile check**

Grep for `OrbitalScanMineralCost` — must be zero hits. Fix stragglers.

- [x] **Step 3: Commit**

```bash
git add Assets/Scripts/Player/ScoutingCardSO.cs Assets/Scripts/Player/CardDeckController.cs Assets/Scripts/MainGame/MainGameCardMap.cs Assets/Resources/Cards/OrbitalScanCard.asset
git commit -m "$(cat <<'EOF'
feat(prologue): Orbital Scan spends energy instead of minerals

EOF
)"
```

---

### Task 3: Connected power graph in `IsPowered` (Tier‑1)

**Files:**
- Modify: `Assets/Scripts/MainGame/MainGameColony.cs`
- Modify: `Assets/Scripts/Tests/MainGameColonyTests.cs`
- Modify: `Assets/Scripts/UI/MainGameHUD.cs`

- [x] **Step 1: Rewrite failing / update storm test**

Replace `DustStorm_DarkensSolar_WindKeepsOxygenPowered` expectations to match **adjacency graph** (already Dist≤1). Add:

```csharp
[Test]
public void IsPowered_CampPowersOxygen_UntilStorm_ThenNeedsAdjacentGenerator()
{
    var c = MainGameColony.CreateLanding();
    c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
    Assert.IsTrue(c.IsPowered(3)); // camp
    c.DebugForceStorm(true);
    Assert.IsFalse(c.IsPowered(3));
    c.DebugPlaceComplete(2, MainGameBuildingId.WindTurbine);
    c.SetDistanceFn((a, b) =>
    {
        if (a == b) return 0;
        if ((a == 3 && b == 2) || (a == 2 && b == 3)) return 1;
        return 99;
    });
    Assert.IsTrue(c.IsPowered(3));
}

[Test]
public void IsPowered_GeneratorTwoHops_ViaConductor()
{
    // Tile 1 Geo — 2 Habitat — 3 Oxygen; Dist ortho 1 between pairs.
    var c = MainGameColony.CreateLanding();
    c.DebugForceStorm(true);
    c.DebugPlaceComplete(1, MainGameBuildingId.WindTurbine);
    c.DebugPlaceComplete(2, MainGameBuildingId.Habitat);
    c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
    c.SetDistanceFn((a, b) =>
    {
        if (a == b) return 0;
        int d = Math.Abs(a - b);
        return d == 1 ? 1 : 99;
    });
    Assert.IsTrue(c.IsPowered(3)); // path Geo-Habitat-Oxygen
}
```

- [x] **Step 2: Implement graph `IsPowered`**

```csharp
public bool IsPowered(int tile)
{
    var b = Board.At(tile).Building;
    if (b == MainGameBuildingId.None) return false;
    var info = MainGameData.Get(b);
    if (!info.NeedsPower) return true;
    return HasPowerPath(tile);
}

bool HasPowerPath(int startTile)
{
    // BFS over finished buildings with Dist <= 1 (Tier-1 tube reach).
    var q = new Queue<int>();
    var seen = new HashSet<int>();
    q.Enqueue(startTile);
    seen.Add(startTile);
    while (q.Count > 0)
    {
        int cur = q.Dequeue();
        var curB = Board.At(cur).Building;
        if (curB == MainGameBuildingId.None || Board.At(cur).Broken) continue;
        if (IsEnergySource(cur)) return true;
        foreach (var t in Board.Tiles)
        {
            if (t.Building == MainGameBuildingId.None || t.Broken) continue;
            if (seen.Contains(t.Index)) continue;
            if (Dist(cur, t.Index) > 1) continue;
            // Under-construction sites do not conduct yet.
            bool building = false;
            foreach (var s in Board.Sites)
                if (s.Tile == t.Index) { building = true; break; }
            if (building) continue;
            seen.Add(t.Index);
            q.Enqueue(t.Index);
        }
    }
    return false;
}

bool IsEnergySource(int tile)
{
    var b = Board.At(tile).Building;
    if (b != MainGameBuildingId.None && MainGameData.Get(b).PowerOutput > 0)
        return true;
    // Camp is a virtual source until storm.
    return !SolarDarkened;
}
```

**Important:** Camp as virtual source means any NeedsPower building is powered pre-storm even if isolated — matching “camp power is power.” During storm, only paths to `PowerOutput > 0` count. Update HUD:

```csharp
static string PowerStatus(MainGameColony colony)
{
    // ...
    if (processors == 0)
        return colony.SolarDarkened ? "storm (camp dark)" : $"camp · {colony.Energy} energy";
    if (colony.SolarDarkened)
        return powered > 0
            ? $"grid · {colony.Energy} energy"
            : "NONE — connect Geothermal by tube";
    return powered > 0
        ? $"camp · {colony.Energy} energy"
        : $"NONE · {colony.Energy} energy";
}
```

- [x] **Step 3: Tests PASS**

- [x] **Step 4: Commit**

```bash
git add Assets/Scripts/MainGame/MainGameColony.cs Assets/Scripts/Tests/MainGameColonyTests.cs Assets/Scripts/UI/MainGameHUD.cs
git commit -m "$(cat <<'EOF'
feat(prologue): Tier-1 connected power graph with camp as source

EOF
)"
```

---

### Task 4: Visual pressurized tubes on MainGame finishes

**Files:**
- Modify: `Assets/Scripts/MainGame/MainGameConstruction.cs`
- Modify: `Assets/Scripts/Units/BaseBuilding.cs` (only if Finish path skips `AutoConnectAdjacentPowerNodes` in MainGame)

- [x] **Step 1: Ensure PowerNode on finish**

In `MainGameConstruction.Finish()` (after scaffolding clear / building complete), call:

```csharp
if (building != null)
{
    if (!building.TryGetComponent<GameDevTV.RTS.Environment.PowerNode>(out _))
        building.gameObject.AddComponent<GameDevTV.RTS.Environment.PowerNode>();
    // Prefer existing completion hook if it already auto-connects:
    building.NotifyMainGamePowerLink(); // add thin public wrapper if AutoConnectAdjacentPowerNodes is private
}
```

If `AutoConnectAdjacentPowerNodes` is private, add:

```csharp
// BaseBuilding.cs
public void NotifyMainGamePowerLink()
{
    if (!TryGetComponent(out PowerNode node))
        gameObject.AddComponent<PowerNode>();
    AutoConnectAdjacentPowerNodes();
}
```

Do **not** rewrite `PowerNode.ConnectTo` / `PressurizedTube`.

- [x] **Step 2: Play-mode verify (Unity CLI on open Editor)**

Enter Play, place Oxygen + Habitat adjacent, End Week until both complete.  
Expected: visible light-blue tube between them; `PowerCord_*` under `PowerGridManager`.

Verified via live Editor: adjacent Command Post + Habitat (Player1) after `NotifyMainGamePowerLink` → `PowerCord_=1`, `PressurizedTube` present, nodes linked.

- [x] **Step 3: Commit**

```bash
git add Assets/Scripts/MainGame/MainGameConstruction.cs Assets/Scripts/Units/BaseBuilding.cs
git commit -m "$(cat <<'EOF'
feat(prologue): auto-link pressurized tubes when builds finish

EOF
)"
```

---

### Task 5: Research state + week costs (TDD)

**Files:**
- Create: `Assets/Scripts/MainGame/MainGameResearch.cs`
- Modify: `Assets/Scripts/MainGame/MainGameData.cs`
- Modify: `Assets/Scripts/MainGame/MainGameColony.cs`
- Modify: `Assets/Scripts/Tests/MainGameColonyTests.cs`

- [x] **Step 1: Failing tests**

```csharp
[Test]
public void Research_CostsWeeks_ThenUnlocksTier()
{
    var c = MainGameColony.CreateLanding();
    c.StartMainGame();
    Assert.IsTrue(c.Research.IsTierUnlocked(MainGameResearchTier.Tier1));
    Assert.IsFalse(c.Research.IsUnlocked(MainGameResearchId.FasterBuild));
    Assert.IsTrue(c.Research.TryBegin(MainGameResearchId.FasterBuild, out _));
    Assert.AreEqual(MainGameData.ResearchWeeks(MainGameResearchId.FasterBuild), c.Research.WeeksRemaining);
    // EndWeek() builds its own log; TickWeek is called inside.
    int need = c.Research.WeeksRemaining;
    for (int i = 0; i < need; i++) c.EndWeek();
    Assert.IsTrue(c.Research.IsUnlocked(MainGameResearchId.FasterBuild));
    Assert.AreEqual(0, c.Research.WeeksRemaining);
}
```

- [x] **Step 2: Implement `MainGameResearch.cs`**

```csharp
namespace GameDevTV.RTS.MainGame
{
    public enum MainGameResearchTier { Tier1 = 1, Tier2 = 2, Tier3 = 3 }

    public enum MainGameResearchId
    {
        None = 0,
        FasterBuild = 1,
        LongerPowerReach = 2,
        ExtraScanType = 3,
    }

    public class MainGameResearch
    {
        readonly HashSet<MainGameResearchId> unlocked = new();
        public MainGameResearchId Active { get; private set; }
        public int WeeksRemaining { get; private set; }

        public bool IsTierUnlocked(MainGameResearchTier tier) =>
            tier == MainGameResearchTier.Tier1 || /* tier2 if any Tier2 id unlocked */ 
            (tier == MainGameResearchTier.Tier2 && unlocked.Count > 0);

        public bool IsUnlocked(MainGameResearchId id) =>
            id == MainGameResearchId.None || unlocked.Contains(id);

        public bool TryBegin(MainGameResearchId id, out string reason)
        {
            reason = null;
            if (id == MainGameResearchId.None) { reason = "No project"; return false; }
            if (unlocked.Contains(id)) { reason = "Already unlocked"; return false; }
            if (Active != MainGameResearchId.None) { reason = "Research already underway"; return false; }
            Active = id;
            WeeksRemaining = MainGameData.ResearchWeeks(id);
            return true;
        }

        public void TickWeek(List<string> log)
        {
            if (Active == MainGameResearchId.None || WeeksRemaining <= 0) return;
            WeeksRemaining--;
            if (WeeksRemaining > 0) return;
            unlocked.Add(Active);
            log?.Add($"Research complete: {Active}");
            Active = MainGameResearchId.None;
        }
    }
}
```

`MainGameData.ResearchWeeks`:

```csharp
public static int ResearchWeeks(MainGameResearchId id) => id switch
{
    MainGameResearchId.FasterBuild => 2,
    MainGameResearchId.LongerPowerReach => 3,
    MainGameResearchId.ExtraScanType => 2,
    _ => 1
};
```

Expose `public MainGameResearch Research { get; }` on colony; construct in `CreateLanding`; call `Research.TickWeek(log)` from `EndWeek`.

**Tier‑1 seating:** all current cards remain unlocked (no flag checks yet).

- [x] **Step 3: Tests PASS + commit**

```bash
git add Assets/Scripts/MainGame/MainGameResearch.cs Assets/Scripts/MainGame/MainGameData.cs Assets/Scripts/MainGame/MainGameColony.cs Assets/Scripts/Tests/MainGameColonyTests.cs
git commit -m "$(cat <<'EOF'
feat(prologue): research projects that complete over End Weeks

EOF
)"
```

---

### Task 6: Research card seating + play → begin project

**Files:**
- Create: `Assets/Scripts/Player/ResearchCardSO.cs` (or extend `ScoutingCardSO` with `ScoutingType.Research` — prefer **new thin `ResearchCardSO : BlueprintCardSO`** to avoid Acts orbital confusion)
- Create: `Assets/Resources/Cards/ResearchCard.asset`
- Modify: `Assets/Scripts/Player/CardDeckController.cs` (`RebuildMainGameHand`)
- Modify: `Assets/Scripts/UI/MainGameBriefingPanel.cs` / note UI if needed for pick target

- [x] **Step 1: Card type**

```csharp
namespace GameDevTV.RTS.Player
{
    [CreateAssetMenu(fileName = "ResearchCard", menuName = "Cards/Research Card")]
    public class ResearchCardSO : BlueprintCardSO
    {
        public override bool CanApply()
        {
            if (!MainGameController.IsActive) return false;
            var c = MainGameController.Instance?.Colony;
            return c != null && c.Research.Active == MainGameResearchId.None;
        }

        public override void Apply()
        {
            var c = MainGameController.Instance?.Colony;
            if (c == null) return;
            // v1: auto-pick first locked Tier-2 id; pause-tree pick is Task 7.
            var pick = MainGameResearchId.FasterBuild;
            if (c.Research.IsUnlocked(pick))
                pick = MainGameResearchId.LongerPowerReach;
            if (!c.Research.TryBegin(pick, out var reason))
            {
                MainGameHUD.Instance?.ShowNote(new List<string> { reason ?? "Cannot research" });
                return;
            }
            MainGameHUD.Instance?.ShowNote(new List<string>
            {
                $"Research started: {pick} ({MainGameData.ResearchWeeks(pick)} wk)."
            });
        }

        public override int GetMaterialsPlayCost() => 0;
    }
}
```

Seat in `RebuildMainGameHand` when `Research.Active == None` and at least one Tier‑2 id still locked (mirror Survey seating pattern). Create asset under `Assets/Resources/Cards/ResearchCard.asset` via Editor or YAML clone of a simple card; set script to `ResearchCardSO`.

- [x] **Step 2: Gate one Tier‑2 effect**

Example — `LongerPowerReach` changes `HasPowerPath` neighbor check from `Dist > 1` to `Dist > (unlocked ? 2 : 1)`.  
`FasterBuild`: `HaulWeeks` or build weeks −1 when unlocked (clamp ≥ 1) in `MainGameData.HaulWeeks` / place path.

- [x] **Step 3: Play-mode — draw Research, play, End Week until complete, confirm unlock log**

- [x] **Step 4: Commit**

```bash
git add Assets/Scripts/Player/ResearchCardSO.cs Assets/Resources/Cards/ResearchCard.asset Assets/Scripts/Player/CardDeckController.cs Assets/Scripts/MainGame/MainGameColony.cs
git commit -m "$(cat <<'EOF'
feat(prologue): research card seats and starts week-long projects

EOF
)"
```

---

### Task 7: Pause tech tree shows Tier‑1 lit / start project pick

**Files:**
- Modify: `Assets/Scripts/UI/PauseMenuUI.cs`
- Modify: `Assets/Scripts/UI/Containers/TechTreeUI.cs` (read-only highlight for MainGame)
- Optional: wire Research card Apply to open pause tree pick instead of auto-pick

- [x] **Step 1: MainGame pause panel**

When `MainGameController.IsActive`, pause menu shows tech ladder text or existing `TechTreeUI` with:
- Tier‑1 nodes marked complete  
- Active research + weeks remaining  
- Locked Tier‑2+ nodes  

If full `TechTreeSO` asset wiring is heavy, ship a **MainGame research list UI** in pause (names + weeks + unlocked) that reads `colony.Research` — still satisfies “pause tree is the map.” Prefer list UI in MainGame to avoid fighting Acts `TechTreeSO` ownership.

```csharp
// PauseMenuUI — when MainGame active, populate a simple vertical list:
// "Connected Power (Tier 1) — unlocked"
// "Faster Build — 2 wk" / "complete" / "in progress 1 wk left"
```

- [x] **Step 2: Research card opens pause list to pick target** (replace auto-pick from Task 6)

- [x] **Step 3: Commit + update PROJECT_KNOWLEDGE**

Document: connected tubes Tier‑1, camp energy, Orbital energy cost, research cards / weeks.

```bash
git add Assets/Scripts/UI/PauseMenuUI.cs PROJECT_KNOWLEDGE.md
git commit -m "$(cat <<'EOF'
feat(prologue): pause research ladder and document power/energy rules

EOF
)"
```

---

## Spec coverage checklist

| Spec item | Task |
|-----------|------|
| Connected power Tier‑1 / reuse tubes | 3, 4 |
| Camp power = energy until generators | 1, 3 |
| Orbital costs energy, no research gate | 2 |
| Research costs weeks only | 5, 6 |
| Research unlocks seating / abilities | 5, 6 |
| Tier‑1 starts unlocked | 5, 7 |
| Pause tree map | 7 |
| No Orbital / basic tube research gate | 2, 3 (no gates added) |
| Solid tubes / manual wires out of scope | — (not in tasks) |

## Out of scope (do not implement in this plan)

- Solid tube upgrade  
- Manual Connect Power as primary UX  
- Energy spend on anything except Orbital (unless a task explicitly adds it)  
- Full Acts `TechTreeSO` content migration  

---
