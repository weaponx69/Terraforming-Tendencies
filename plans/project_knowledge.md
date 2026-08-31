# Terriforming Tendencies Project Knowledge

## Health Bar System
- **Purpose**: Visual representation of colony integrity (health) in the UI
- **Components**:
  - `HealthBar.cs`: C# script managing health percentage calculation
  - `ProgressBar.prefab`: UI element displaying the health bar
  - `HealthBarContainer.prefab`: Container for positioning the health bar in the UI
- **Integration**:
  - Health percentage calculated via `currentHealth / maxHealth`
  - Progress bar updates in real-time through `SetProgress()` method

## Visual Scripting Implementation
- **HealthBarLogic.asset**: Visual script node for health bar logic
- **Key Nodes**:
  - `ExposedReference` for health bar object and health values
  - `OnBehaviourPlay` method for real-time updates
  - `UnityEngine.UI.Image` component for visual representation

## Testing
- **Test Case**: Verify health percentage updates when `currentHealth` changes
- **Expected Behavior**: Progress bar reflects current integrity percentage accurately

## Notes
- Health bar is positioned in the UI container on the left side
- Progress bar uses `fillAmount` for percentage display
- Visual script nodes are connected to the Progress Bar prefab

## Dependencies
- Requires Unity UI Toolkit
- Uses `UnityEngine.UI.Image` component for rendering
- Visual script nodes must be assigned in the Unity Editor

## Integrity Bar ↔ Colony Health

### Authoritative calculation
1. **`GlobalDecayManager.DecayLoop()`** damages buildings/units outside life support every tick **only after** `Supplies.ColonyIntegrityActive` is true
2. **`Supplies.CalculateIntegrity()`** aggregates `CurrentHealth / MaxHealth` for player-owned commandables; returns **100%** until integrity is active
3. **`Supplies.UpdateIntegrity()`** fires `OnIntegrityChanged`
4. **`ColonyIntegrityBar`** subscribes and updates the HUD fill

### Excluded from integrity (do not count toward the bar)
- **`GlobalCommander`** / **Universal Command Center** — invulnerable hub (99999 HP)
- **`DecayStarter`** — hidden legacy stand-in for the UCC; must never inflate integrity
- Any commandable with `MaxHealth >= 90000`

### When integrity starts counting
- Flag: `Supplies.ColonyIntegrityActive` (false on scene load / Supplies Awake)
- Set by `BeginColonyIntegrityIfNeeded` from `BaseBuilding.CompleteConstruction` on the first real `(Clone)` building that counts toward integrity
- Until then decay ticks and integrity recalculation are skipped

### Bug fixed (2026-06-30)
`SurvivalManager` was recalculating integrity from all commandables every second and overwriting per-tick writes. It now only drains biomass; integrity is owned by `GlobalDecayManager` + `CalculateIntegrity()`.

### Key files
- `Assets/Scripts/Environment/GlobalDecayManager.cs` — decay ticks + integrity refresh (gated)
- `Assets/Scripts/Player/Supplies.cs` — `CalculateIntegrity()` + `CountsTowardIntegrity()` + `BeginColonyIntegrityIfNeeded`
- `Assets/Scripts/UI/Components/ColonyIntegrityBar.cs` — HUD bar
- `Assets/Scripts/Environment/DecayStarter.cs` — legacy stand-in (excluded from calculation)

See also `PROJECT_KNOWLEDGE.md` **§39 Colony Integrity Start Gate**.

---

## Card System — Two Parallel Architectures

There are **two card systems** that coexist in the codebase. Both provide card-based gameplay but use different data models and rendering paths.

### System A: `CardSO` / `CardDeckSO` / `CardDeckManager` (v2 Tech Tree Cards)

| Aspect | Detail |
|---|---|
| **Purpose** | Tech-tree-gated card system with weighted random draws, draw costs, and direct supply effects. |
| **Namespace** | `GameDevTV.RTS.TechTree` (data), `GameDevTV.RTS.Player` (manager) |
| **Enums** | `CardRarity` (`Common`, `Uncommon`, `Rare`, `Epic`), `CardEffectType` (`None`, `Biomass`, `Oxygen`, `Power`, `Population`, `Materials`, `Temperature`, `Atmosphere`, `Water`, `CommandPost`) |
| **Data Model** | `CardSO` (ScriptableObject) — `CardName`, `Icon`, `Description`, `WrappedUpgrade` (UpgradeSO), `Rarity`, `DrawWeight`, `PlayCost`, `EffectType`, `EffectAmount` |
| **Deck Config** | `CardDeckSO` — `AllCards` list, `HandSize` (5), `MaxHandSize` (7), `DrawCost` (50), `RefreshOnNewGeneration`. `BuildDrawPool(Owner)` filters by tech tree unlock status. |
| **Runtime Manager** | `CardDeckManager` (singleton MonoBehaviour) — Manages `DrawPool`, `Hand`, `DiscardPile`. Methods: `RefreshDeck()`, `BuildDrawPool()`, `DrawHand()` (weighted random), `PlayCard(CardSO)`, `CanPlayCard(CardSO)`, `ApplyCardEffect(CardSO)`. Events: `OnHandChanged`, `OnCardPlayed`, `OnDeckRefreshed`. |
| **Draw Mechanics** | Weighted random from `DrawPool` using `DrawWeight` field. Costs `DrawCost` Materials. Cannot draw if hand is at `MaxHandSize`. |
| **Play Mechanics** | Checks: card in hand, tech tree unlocked + not researched, enough Materials. Deducts `PlayCost`, fires `UpgradeResearchedEvent`, applies direct supply effect, moves to discard. |
| **UI** | `CardDeckUI` (container) + `CardUI` (individual card) — Shows hand, rarity-colored borders, play button with interactivity check |
| **Card Assets** | 28+ `.asset` files in `Assets/Resources/Cards/` |

### System B: `BlueprintCardSO` Hierarchy / `CardDeckController` (Original Hand System)

| Aspect | Detail |
|---|---|
| **Purpose** | Original card system where cards directly represent actions (unlock building, spawn unit, grant resources, apply buff). Used by bottom bar. |
| **Namespace** | `GameDevTV.RTS.Player` |
| **Base Class** | `BlueprintCardSO` (abstract, ScriptableObject) — `cardName`, `cardDescription`, `icon`, `HazardEventPrefab`. Abstract: `Apply()`. Virtual: `IsGateMet()` (default true), `GetCardGoal()`. |

#### BlueprintCardSO Subclasses

| Class | Purpose | Key Fields | `Apply()` Effect |
|---|---|---|---|
| `UnlockBuildingCardSO` | Unlock a building for construction | `buildingToUnlock` (BuildingSO) | Calls `BlueprintDraftManager.UnlockBuilding(name)` |
| `TerraformingCardSO` | Weather-gated building unlock | Extends UnlockBuilding with climate gates: `minTemperature`, `maxTemperature`, `minOxygen`, `maxOxygen`, `minAtmosphere`, `maxAtmosphere`, `minWater`, `maxWater`, `requiredSectorFeature` | `IsGateMet()` checks current Supplies values; `Apply()` also calls `RegisterBuildingSO()` |
| `SpawnUnitCardSO` | Spawn a free unit | `unitPrefab` (GameObject) | Instantiates prefab at command post position (or camera center) |
| `ResourceShipmentCardSO` | Grant resources | `materialsAmount`, `biomassAmount`, `oxygenAmount`, `temperatureAmount`, `atmosphereAmount`, `waterAmount` | Adds to Supplies + optionally sets ClimateManager targets |
| `PassiveBuffCardSO` | Apply passive multiplier | `BuffType` (GatherSpeed, PowerGeneration), `multiplier` (default 1.2) | Multiplies `BlueprintDraftManager.GatherSpeedMultiplier` or `PowerGenMultiplier` |
| `DiscoveryCardSO` | Reveal resource deposits | `DiscoveryType` (IronVein, GasPocket, RegolithField, MineralSurvey, DeepCoreScan, DebrisField), `bonusMaterials` | Calls `DiscoverySystem.RevealResourceType()` or enables salvage |
| `ScoutingCardSO` | Exploration/scouting actions | `ScoutingType` (OrbitalScan, PipelineBoost, SurveyDrone, EmergencyCaches), `materialsAmount` | Interacts with `ExplorationManager` (InstantExplore, Boost, DeploySurveyDrone) |
| `DrillBreakthroughCardSO` | Mining speed multiplier | `gatherSpeedMultiplier` (default 1.5) | Multiplies `BlueprintDraftManager.GatherSpeedMultiplier` |

#### CardDeckController (Runtime Manager for System B)

| Aspect | Detail |
|---|---|
| **Nature** | Auto-spawning singleton (`RuntimeInitializeOnLoadMethod.BeforeSceneLoad`) |
| **Hand Size** | 10 cards |
| **Deck Management** | `masterDeck`, `drawPile`, `discardPile`, `hand` |
| **Starting Hand** | `RebuildDeck()` seeds Command Post + Solar (Mining Drone if playable), then FIFO-fills remaining slots |
| **Draw Mechanics** | **FIFO queue** — front of `drawPile`; played/skipped cards go to back of discard; recycle preserves order (**no shuffle**, no priority promotion). See `PROJECT_KNOWLEDGE.md` §37. |
| **Play Mechanics** | `PlayCard` / `ConsumeCardAfterBuild` → `Apply()`, discard, `FillHand()` |
| **Draft System** | **Disabled** — `TriggerDraft()` is a no-op; player uses the normal hand |
| **Events** | `OnHandChanged` |
| **Goal colors** | Sector-completion goals only (`TerraformingGoalColors`) — Temp/Atmos/Water + milestone types; support cards stay neutral. See `PROJECT_KNOWLEDGE.md` §38. |

### BlueprintDraftManager (Static Support for System B)

| Aspect | Detail |
|---|---|
| **Nature** | Static class managing unlocked buildings, buff multipliers, and draft completion |
| **Starting Unlocks** | Command Post, Supply Hut, Solar Panel, Oxygen Processor, GHG Factory, Water Ice Aquifer, Subglacial Water Extractor |
| **Key Methods** | `UnlockBuilding(name)`, `LockBuilding(name)`, `CompleteDraft(card)`, `RegisterBuildingSO(building)`, `GetBuildingSOByName(name)` |
| **Static State** | `GatherSpeedMultiplier` (default 1.0), `PowerGenMultiplier` (default 1.0), `SalvageEnabled` (default false) |
| **Reset** | On scene load, resets to default starting unlocks and loads all BuildingSO from Resources |

---

## UI Layer — Card & Ability UIs

### System A UI (v2 CardDeck Manager)

| File | Purpose |
|---|---|
| `Assets/Scripts/UI/Containers/CardDeckUI.cs` | Shows CardDeckManager's hand, draw button, materials display. Subscribes to `OnHandChanged`, `OnCardPlayed`, `Supplies.OnMaterialsChanged`. |
| `Assets/Scripts/UI/Components/CardUI.cs` | Individual card with icon, rarity-colored border, name, cost, effect description, play button. Checks `CardDeckManager.CanPlayCard()` for interactivity. Rarity colors: Common=gray, Uncommon=green, Rare=blue, Epic=purple. |

### System B UI (BlueprintCardSO / Bottom Bar)

| File | Purpose |
|---|---|
| `Assets/Scripts/UI/Containers/BottomBarActionsUI.cs` | Persistent bottom bar showing 10-card hand from `CardDeckController`. Building cards → reserved-site build; others → `PlayCardCommand`. Sector-completion cards get goal color accents. |
| `Assets/Scripts/UI/Containers/BlueprintDraftUI.cs` | Loads `Resources/Cards` into the deck and calls `RebuildDeck()`. Full-screen draft selection is **disabled**. |
| `Assets/Scripts/UI/CardSlotUI.cs` | Draft card slot (legacy); colors sector-goal titles when used. |
| `Assets/Scripts/UI/Containers/ActiveObjectivesUI.cs` | Sector goal + Temp/Atmos/Water with shared goal colors. |
| `Assets/Scripts/UI/TerraformingGoalColors.cs` | Shared palette for sector-completion goals only. |
| `Assets/Scripts/UI/Containers/AbilityHandUI.cs` | Persistent ability hand showing `ActiveAbilityCommand` instances from owned completed/operational buildings. |
| `Assets/Scripts/UI/Components/AbilityCardSlotUI.cs` | Ability card with cooldown overlay, lock overlay, percentage text, hover effects. |
| `Assets/Scripts/Commands/PlayCardCommand.cs` | Wraps `CardDeckController.PlayCard(HandIndex)`. |

---

### Card Data Flow Summary

```
Game Start
  ├─ BlueprintDraftUI.InitializeDefaultPool() → load Resources/Cards
  │   └─ CardDeckController.RebuildDeck() → FIFO draw pile + seed CP/Solar → OnHandChanged
  ├─ BottomBarActionsUI shows hand (sector-goal colors on terraforming cards)
  │       ├─ Building cards → reserved-site selection / instant pad build
  │       └─ Other cards → PlayCardCommand → FillHand (FIFO)
  └─ Draft overlays / TriggerDraft are disabled
```

---

## Editor Tools

| File | Purpose |
|---|---|
| `Assets/Editor/CardDeckSetup.cs` | Menu items under `Tools/Card Deck/` — `Create Card Assets from Unlockables`, `Setup Main Deck` |

---

## Version History
- 2026-06-29: Initial implementation of health bar system
- 2026-06-29: Visual script version created for easier maintenance
- 2026-06-30: Fixed SurvivalManager overwriting DecayStarter integrity writes; switched to drain-based approach
- 2026-07-08: Documented dual card system architecture
- 2026-08-31: FIFO deck draw; integrity starts after first building; sector-goal colors only for completion terraforming; draft rounds disabled
