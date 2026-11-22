# Research + Connected Power Implementation Plan

> **For agentic workers:** implement this plan task-by-task, in order, checking off each step as it lands. Steps use checkbox (`- [ ]`) syntax for tracking. Dispatch a fresh subagent per task where the tasks are independent (see dispatching-parallel-agents), otherwise execute inline with a review checkpoint after each task.

**Goal:** Prologue uses real connected power (existing pressurized tubes) from Tier‑1, a camp→grid energy pool that Orbital Scan spends, and research cards that cost weeks to unlock higher-tier seating.

**Architecture:** Keep board rules in `PrologueColony` (EditMode-testable): adjacency power graph + energy pool. Drive visuals by ensuring finished Prologue `BaseBuilding`s get `PowerNode` auto-links (existing `ConnectTo` / `PressurizedTube`). Research is a small Prologue state machine (active project + weeks + unlock set) that gates hand seating and lights the pause tech tree; Tier‑1 starts unlocked.

**Tech Stack:** Unity 6000.4, existing `PowerNode` / `PressurizedTube` / `TechTreeSO`, NUnit EditMode tests, Unity CLI on the open Editor.

**Spec:** [`docs/specs/2026-11-22-research-connected-power-design.md`](../specs/2026-11-22-research-connected-power-design.md)

---

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Prologue/PrologueData.cs` | `CampEnergyGeneration`, `OrbitalScanEnergyCost`, research week costs; remove mineral Orbital cost |
| `Assets/Scripts/Prologue/PrologueColony.cs` | Energy pool, `IsPowered` via adjacency graph, research progress / unlocks |
| `Assets/Scripts/Prologue/PrologueResearch.cs` | (new) Research card target ids, seating flags, week tick helper |
| `Assets/Scripts/Player/ScoutingCardSO.cs` | Orbital spends energy; `CanApply` / `Apply` / play cost display |
| `Assets/Scripts/Player/CardDeckController.cs` | Orbital spend path; seat Research card; gate Tier‑2+ cards |
| `Assets/Scripts/Prologue/PrologueCardMap.cs` | Orbital energy (not minerals) for cost display |
| `Assets/Scripts/UI/PrologueHUD.cs` | Show energy + real power status |
| `Assets/Scripts/Prologue/PrologueConstruction.cs` | On finish: ensure `PowerNode` + `AutoConnect` path fires |
| `Assets/Scripts/UI/PauseMenuUI.cs` / `TechTreeUI.cs` | Show Prologue tech ladder with Tier‑1 lit |
| `Assets/Resources/Cards/OrbitalScanCard.asset` | Description: energy cost |
| `Assets/Resources/Cards/ResearchCard.asset` | (new) Research scouting/blueprint card |
| `Assets/Scripts/Tests/PrologueColonyTests.cs` | Energy, graph power, research weeks |
| `PROJECT_KNOWLEDGE.md` | Document shipped behavior |

---

### Task 1: Energy pool constants + colony API (TDD)

**Files:**
- Modify: `Assets/Scripts/Prologue/PrologueData.cs`
- Modify: `Assets/Scripts/Prologue/PrologueColony.cs`
- Modify: `Assets/Scripts/Tests/PrologueColonyTests.cs`

- [x] **Step 1: Write failing tests**

Add to `PrologueColonyTests.cs`:

```csharp
[Test]
public void Energy_CampFillsPool_OrbitalSpends()
{
    var c = PrologueColony.CreateLanding();
    c.StartPrologue();
    Assert.GreaterOrEqual(c.Energy, PrologueData.OrbitalScanEnergyCost);
    Assert.IsTrue(c.TrySpendEnergy(PrologueData.OrbitalScanEnergyCost));
    int after = c.Energy;
    Assert.IsFalse(c.TrySpendEnergy(c.Energy + 1));
    Assert.AreEqual(after, c.Energy);
}

[Test]
public void Energy_RefillsFromCampOnEndWeek_StormCutsCamp()
{
    var c = PrologueColony.CreateLanding();
    c.StartPrologue();
    while (c.TrySpendEnergy(1)) { }
    Assert.AreEqual(0, c.Energy);
    c.EndWeek();
    Assert.AreEqual(PrologueData.CampEnergyGeneration, c.Energy);
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

In `PrologueData.cs`, add energy constants (keep `OrbitalScanMineralCost` until Task 2):

```csharp
/// <summary>Camp grid fill per End Week while storm has not darkened camp.</summary>
public const int CampEnergyGeneration = 8;
/// <summary>Orbital Scan spend from the energy pool (not minerals).</summary>
public const int OrbitalScanEnergyCost = 5;
```

`OrbitalScanMineralCost` stays until Task 2 migrates Orbital play path / greps clean.

In `PrologueColony.cs` (near stocks):

```csharp
public int Energy { get; private set; }

public int EnergyGeneration()
{
    int gen = SolarDarkened ? 0 : PrologueData.CampEnergyGeneration;
    foreach (var t in Board.Tiles)
    {
        if (t.Building == PrologueBuildingId.None || t.Broken) continue;
        // Sites still building do not generate.
        bool underConstruction = false;
        foreach (var s in Board.Sites)
            if (s.Tile == t.Index) { underConstruction = true; break; }
        if (underConstruction) continue;
        gen += PrologueData.Get(t.Building).PowerOutput;
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

Call `RefillEnergy()` at end of `CreateLanding` / `StartPrologue`, and in `EndWeek` after storm flags update (match test: refill on EndWeek after drain).

- [x] **Step 4: Re-run tests — expect PASS**

- [x] **Step 5: Commit**

```bash
git add Assets/Scripts/Prologue/PrologueData.cs Assets/Scripts/Prologue/PrologueColony.cs Assets/Scripts/Tests/PrologueColonyTests.cs
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
- Modify: `Assets/Scripts/Prologue/PrologueCardMap.cs`
- Modify: `Assets/Resources/Cards/OrbitalScanCard.asset` (description text only)
- Modify: `Assets/Scripts/UI/PrologueHUD.cs` (optional note path already in Apply)

- [x] **Step 1: Update CanApply / GetMaterialsPlayCost / Apply**

In `ScoutingCardSO.cs` Prologue Orbital branches:

```csharp
// CanApply
return colony.Energy >= PrologueData.OrbitalScanEnergyCost;

// GetMaterialsPlayCost — Orbital no longer uses minerals in Prologue
return 0;

// Apply
int cost = PrologueData.OrbitalScanEnergyCost;
if (!colony.TrySpendEnergy(cost)) return;
// ... PingNearestIce ...
PrologueHUD.Instance?.ShowNote(... $"Orbital Scan (−{cost} energy)." ...);
```

In `CardDeckController.cs`, replace any `OrbitalScanMineralCost` / `TrySpendStock(Minerals)` Orbital path with `TrySpendEnergy` / `OrbitalScanEnergyCost`.

In `PrologueCardMap.MineralCost`, Orbital returns `0` (energy is not minerals). Add a helper if UI needs it:

```csharp
public static int EnergyCost(BlueprintCardSO card)
{
    if (card is ScoutingCardSO scout
        && scout.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan)
        return PrologueData.OrbitalScanEnergyCost;
    return 0;
}
```

Update `OrbitalScanCard.asset` description to say energy, not minerals.

- [x] **Step 2: Compile check**

Grep for `OrbitalScanMineralCost` — must be zero hits. Fix stragglers.

- [x] **Step 3: Commit**

```bash
git add Assets/Scripts/Player/ScoutingCardSO.cs Assets/Scripts/Player/CardDeckController.cs Assets/Scripts/Prologue/PrologueCardMap.cs Assets/Resources/Cards/OrbitalScanCard.asset
git commit -m "$(cat <<'EOF'
feat(prologue): Orbital Scan spends energy instead of minerals

EOF
)"
```

---

### Task 3: Connected power graph in `IsPowered` (Tier‑1)

**Files:**
- Modify: `Assets/Scripts/Prologue/PrologueColony.cs`
- Modify: `Assets/Scripts/Tests/PrologueColonyTests.cs`
- Modify: `Assets/Scripts/UI/PrologueHUD.cs`

- [x] **Step 1: Rewrite failing / update storm test**

Replace `DustStorm_DarkensSolar_WindKeepsOxygenPowered` expectations to match **adjacency graph** (already Dist≤1). Add:

```csharp
[Test]
public void IsPowered_CampPowersOxygen_UntilStorm_ThenNeedsAdjacentGenerator()
{
    var c = PrologueColony.CreateLanding();
    c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
    Assert.IsTrue(c.IsPowered(3)); // camp
    c.DebugForceStorm(true);
    Assert.IsFalse(c.IsPowered(3));
    c.DebugPlaceComplete(2, PrologueBuildingId.WindTurbine);
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
    var c = PrologueColony.CreateLanding();
    c.DebugForceStorm(true);
    c.DebugPlaceComplete(1, PrologueBuildingId.WindTurbine);
    c.DebugPlaceComplete(2, PrologueBuildingId.Habitat);
    c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
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
    if (b == PrologueBuildingId.None) return false;
    var info = PrologueData.Get(b);
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
        if (curB == PrologueBuildingId.None || Board.At(cur).Broken) continue;
        if (IsEnergySource(cur)) return true;
        foreach (var t in Board.Tiles)
        {
            if (t.Building == PrologueBuildingId.None || t.Broken) continue;
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
    if (b != PrologueBuildingId.None && PrologueData.Get(b).PowerOutput > 0)
        return true;
    // Camp is a virtual source until storm.
    return !SolarDarkened;
}
```

**Important:** Camp as virtual source means any NeedsPower building is powered pre-storm even if isolated — matching “camp power is power.” During storm, only paths to `PowerOutput > 0` count. Update HUD:

```csharp
static string PowerStatus(PrologueColony colony)
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
git add Assets/Scripts/Prologue/PrologueColony.cs Assets/Scripts/Tests/PrologueColonyTests.cs Assets/Scripts/UI/PrologueHUD.cs
git commit -m "$(cat <<'EOF'
feat(prologue): Tier-1 connected power graph with camp as source

EOF
)"
```

---

### Task 4: Visual pressurized tubes on Prologue finishes

**Files:**
- Modify: `Assets/Scripts/Prologue/PrologueConstruction.cs`
- Modify: `Assets/Scripts/Units/BaseBuilding.cs` (only if Finish path skips `AutoConnectAdjacentPowerNodes` in Prologue)

- [x] **Step 1: Ensure PowerNode on finish**

In `PrologueConstruction.Finish()` (after scaffolding clear / building complete), call:

```csharp
if (building != null)
{
    if (!building.TryGetComponent<GameDevTV.RTS.Environment.PowerNode>(out _))
        building.gameObject.AddComponent<GameDevTV.RTS.Environment.PowerNode>();
    // Prefer existing completion hook if it already auto-connects:
    building.NotifyProloguePowerLink(); // add thin public wrapper if AutoConnectAdjacentPowerNodes is private
}
```

If `AutoConnectAdjacentPowerNodes` is private, add:

```csharp
// BaseBuilding.cs
public void NotifyProloguePowerLink()
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

Verified via live Editor: adjacent Command Post + Habitat (Player1) after `NotifyProloguePowerLink` → `PowerCord_=1`, `PressurizedTube` present, nodes linked.

- [x] **Step 3: Commit**

```bash
git add Assets/Scripts/Prologue/PrologueConstruction.cs Assets/Scripts/Units/BaseBuilding.cs
git commit -m "$(cat <<'EOF'
feat(prologue): auto-link pressurized tubes when builds finish

EOF
)"
```

---

### Task 5: Research state + week costs (TDD)

**Files:**
- Create: `Assets/Scripts/Prologue/PrologueResearch.cs`
- Modify: `Assets/Scripts/Prologue/PrologueData.cs`
- Modify: `Assets/Scripts/Prologue/PrologueColony.cs`
- Modify: `Assets/Scripts/Tests/PrologueColonyTests.cs`

- [x] **Step 1: Failing tests**

```csharp
[Test]
public void Research_CostsWeeks_ThenUnlocksTier()
{
    var c = PrologueColony.CreateLanding();
    c.StartPrologue();
    Assert.IsTrue(c.Research.IsTierUnlocked(PrologueResearchTier.Tier1));
    Assert.IsFalse(c.Research.IsUnlocked(PrologueResearchId.FasterBuild));
    Assert.IsTrue(c.Research.TryBegin(PrologueResearchId.FasterBuild, out _));
    Assert.AreEqual(PrologueData.ResearchWeeks(PrologueResearchId.FasterBuild), c.Research.WeeksRemaining);
    // EndWeek() builds its own log; TickWeek is called inside.
    int need = c.Research.WeeksRemaining;
    for (int i = 0; i < need; i++) c.EndWeek();
    Assert.IsTrue(c.Research.IsUnlocked(PrologueResearchId.FasterBuild));
    Assert.AreEqual(0, c.Research.WeeksRemaining);
}
```

- [x] **Step 2: Implement `PrologueResearch.cs`**

```csharp
namespace GameDevTV.RTS.Prologue
{
    public enum PrologueResearchTier { Tier1 = 1, Tier2 = 2, Tier3 = 3 }

    public enum PrologueResearchId
    {
        None = 0,
        FasterBuild = 1,
        LongerPowerReach = 2,
        ExtraScanType = 3,
    }

    public class PrologueResearch
    {
        readonly HashSet<PrologueResearchId> unlocked = new();
        public PrologueResearchId Active { get; private set; }
        public int WeeksRemaining { get; private set; }

        public bool IsTierUnlocked(PrologueResearchTier tier) =>
            tier == PrologueResearchTier.Tier1 || /* tier2 if any Tier2 id unlocked */ 
            (tier == PrologueResearchTier.Tier2 && unlocked.Count > 0);

        public bool IsUnlocked(PrologueResearchId id) =>
            id == PrologueResearchId.None || unlocked.Contains(id);

        public bool TryBegin(PrologueResearchId id, out string reason)
        {
            reason = null;
            if (id == PrologueResearchId.None) { reason = "No project"; return false; }
            if (unlocked.Contains(id)) { reason = "Already unlocked"; return false; }
            if (Active != PrologueResearchId.None) { reason = "Research already underway"; return false; }
            Active = id;
            WeeksRemaining = PrologueData.ResearchWeeks(id);
            return true;
        }

        public void TickWeek(List<string> log)
        {
            if (Active == PrologueResearchId.None || WeeksRemaining <= 0) return;
            WeeksRemaining--;
            if (WeeksRemaining > 0) return;
            unlocked.Add(Active);
            log?.Add($"Research complete: {Active}");
            Active = PrologueResearchId.None;
        }
    }
}
```

`PrologueData.ResearchWeeks`:

```csharp
public static int ResearchWeeks(PrologueResearchId id) => id switch
{
    PrologueResearchId.FasterBuild => 2,
    PrologueResearchId.LongerPowerReach => 3,
    PrologueResearchId.ExtraScanType => 2,
    _ => 1
};
```

Expose `public PrologueResearch Research { get; }` on colony; construct in `CreateLanding`; call `Research.TickWeek(log)` from `EndWeek`.

**Tier‑1 seating:** all current cards remain unlocked (no flag checks yet).

- [x] **Step 3: Tests PASS + commit**

```bash
git add Assets/Scripts/Prologue/PrologueResearch.cs Assets/Scripts/Prologue/PrologueData.cs Assets/Scripts/Prologue/PrologueColony.cs Assets/Scripts/Tests/PrologueColonyTests.cs
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
- Modify: `Assets/Scripts/Player/CardDeckController.cs` (`RebuildPrologueHand`)
- Modify: `Assets/Scripts/UI/PrologueBriefingPanel.cs` / note UI if needed for pick target

- [ ] **Step 1: Card type**

```csharp
namespace GameDevTV.RTS.Player
{
    [CreateAssetMenu(fileName = "ResearchCard", menuName = "Cards/Research Card")]
    public class ResearchCardSO : BlueprintCardSO
    {
        public override bool CanApply()
        {
            if (!PrologueController.IsActive) return false;
            var c = PrologueController.Instance?.Colony;
            return c != null && c.Research.Active == PrologueResearchId.None;
        }

        public override void Apply()
        {
            var c = PrologueController.Instance?.Colony;
            if (c == null) return;
            // v1: auto-pick first locked Tier-2 id; pause-tree pick is Task 7.
            var pick = PrologueResearchId.FasterBuild;
            if (c.Research.IsUnlocked(pick))
                pick = PrologueResearchId.LongerPowerReach;
            if (!c.Research.TryBegin(pick, out var reason))
            {
                PrologueHUD.Instance?.ShowNote(new List<string> { reason ?? "Cannot research" });
                return;
            }
            PrologueHUD.Instance?.ShowNote(new List<string>
            {
                $"Research started: {pick} ({PrologueData.ResearchWeeks(pick)} wk)."
            });
        }

        public override int GetMaterialsPlayCost() => 0;
    }
}
```

Seat in `RebuildPrologueHand` when `Research.Active == None` and at least one Tier‑2 id still locked (mirror Survey seating pattern). Create asset under `Assets/Resources/Cards/ResearchCard.asset` via Editor or YAML clone of a simple card; set script to `ResearchCardSO`.

- [ ] **Step 2: Gate one Tier‑2 effect**

Example — `LongerPowerReach` changes `HasPowerPath` neighbor check from `Dist > 1` to `Dist > (unlocked ? 2 : 1)`.  
`FasterBuild`: `HaulWeeks` or build weeks −1 when unlocked (clamp ≥ 1) in `PrologueData.HaulWeeks` / place path.

- [ ] **Step 3: Play-mode — draw Research, play, End Week until complete, confirm unlock log**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Player/ResearchCardSO.cs Assets/Resources/Cards/ResearchCard.asset Assets/Scripts/Player/CardDeckController.cs Assets/Scripts/Prologue/PrologueColony.cs
git commit -m "$(cat <<'EOF'
feat(prologue): research card seats and starts week-long projects

EOF
)"
```

---

### Task 7: Pause tech tree shows Tier‑1 lit / start project pick

**Files:**
- Modify: `Assets/Scripts/UI/PauseMenuUI.cs`
- Modify: `Assets/Scripts/UI/Containers/TechTreeUI.cs` (read-only highlight for Prologue)
- Optional: wire Research card Apply to open pause tree pick instead of auto-pick

- [ ] **Step 1: Prologue pause panel**

When `PrologueController.IsActive`, pause menu shows tech ladder text or existing `TechTreeUI` with:
- Tier‑1 nodes marked complete  
- Active research + weeks remaining  
- Locked Tier‑2+ nodes  

If full `TechTreeSO` asset wiring is heavy, ship a **Prologue research list UI** in pause (names + weeks + unlocked) that reads `colony.Research` — still satisfies “pause tree is the map.” Prefer list UI in Prologue to avoid fighting Acts `TechTreeSO` ownership.

```csharp
// PauseMenuUI — when Prologue active, populate a simple vertical list:
// "Connected Power (Tier 1) — unlocked"
// "Faster Build — 2 wk" / "complete" / "in progress 1 wk left"
```

- [ ] **Step 2: Research card opens pause list to pick target** (replace auto-pick from Task 6)

- [ ] **Step 3: Commit + update PROJECT_KNOWLEDGE**

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
