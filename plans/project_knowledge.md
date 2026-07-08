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

## Integrity Bar ↔ DecayStarter Wiring

### Full Event Chain
1. **`DecayStarter.Update()`** fires every 0.1s → calls `TakeDamage(5)` on itself (500 max HP) → calls `Supplies.UpdateIntegrity(Owner, ratio * 100f)` where ratio = `CurrentHealth / MaxHealth`
2. **`Supplies.UpdateIntegrity()`** clamps to 0 and fires `Supplies.OnIntegrityChanged`
3. **`ColonyIntegrityBar.HandleIntegrityChanged()`** sets `_targetFill = newValue / 100f`
4. **`ColonyIntegrityBar.Update()`** lerps `_currentFill` → `_targetFill` and sets `fillImage.fillAmount` + applies color transitions

### Bug Fixed (2026-06-30)
`SurvivalManager.SurvivalLoop()` was calling `Supplies.CalculateIntegrity()` (aggregates ALL commandable HP — mostly full → ~100%) and writing that back every second, **overwriting** `DecayStarter`'s per-tick writes and keeping the bar near 100%.

**Fix**: `SurvivalManager` now drains integrity by `integrityDrainRate * tickRate` from the current value each tick, rather than recalculating from commandable health. This means:
- `DecayStarter` writes drive the bar directly without being overwritten
- `SurvivalManager` provides an additional constant drain on top
- Both drain in the same direction; neither resets

### Key Files
- `Assets/Scripts/Environment/DecayStarter.cs` — ticks integrity down via its own HP ratio
- `Assets/Scripts/UI/Components/ColonyIntegrityBar.cs` — `Filled` Image bar subscribed to `Supplies.OnIntegrityChanged`
- `Assets/Scripts/Player/SurvivalManager.cs` — drain-based loop (NOT recalculate-based)
- `Assets/Scripts/Player/Supplies.cs` — `UpdateIntegrity()` + `OnIntegrityChanged` event

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
| **Deck Management** | `masterDeck` (BlueprintCardSO list), `drawPile`, `discardPile`, `hand` |
| **Starting Hand** | `RebuildDeck()` — 3 guaranteed starters (Command Post, Solar Panel, Mining Drone) at indices 0-2, then fills remaining 7 slots from draw pile |
| **Play Mechanics** | `PlayCard(int handIndex)` — registers hazard if present, calls `card.Apply()`, removes from hand, discards, draws replacement via `FillHand()` |
| **Draft System** | `TriggerDraft()` — pauses game, curates hand for draft (prefers scouting cards for locked sectors), fires `OnDraftStarted` event. `SelectCard()` completes draft via `BlueprintDraftManager.CompleteDraft()`. |
| **Events** | `OnHandChanged`, `OnDraftStarted` |
| **Integration** | `SectorManager.OnSectorUnlocked` triggers a draft. `UpgradeResearchedEvent` refreshes bottom bar. |

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
| `Assets/Scripts/UI/Containers/BottomBarActionsUI.cs` | Persistent bottom bar showing 10-card hand from `CardDeckController`. Building cards create `BuildBuildingCommand` for placement mode; non-building cards use `PlayCardCommand`. Falls back to GlobalCommander unlocked buildings for empty slots. |
| `Assets/Scripts/UI/Containers/BlueprintDraftUI.cs` | Draft selection panel shown at generation start. Pauses game (Time.timeScale=0), shows 3 random cards from pool, displays building stats and terraforming requirements. |
| `Assets/Scripts/UI/CardSlotUI.cs` | Draft card slot with hover scale animation (1.07x) and glow outline. Calls selection callback on click. |
| `Assets/Scripts/UI/Containers/AbilityHandUI.cs` | Persistent ability hand showing `ActiveAbilityCommand` instances from owned completed/operational buildings. Auto-collects via `BaseBuilding.ActiveBuildings`. |
| `Assets/Scripts/UI/Components/AbilityCardSlotUI.cs` | Ability card with cooldown overlay (fillAmount), lock overlay, percentage text, hover effects. |
| `Assets/Scripts/Commands/PlayCardCommand.cs` | Simple command wrapping `CardDeckController.PlayCard(HandIndex)`. `RequiresClickToActivate = false` for immediate fire. |

---

### Card Data Flow Summary

```
Generation Starts
  ├─ BlueprintDraftUI.ShowDraftSelection() → pause, show 3 cards
  │   └─ Player picks → BlueprintDraftManager.CompleteDraft() → card.Apply()
  │
  ├─ CardDeckController.RebuildDeck() → 3 guaranteed + 7 random → OnHandChanged
  │   └─ BottomBarActionsUI.RefreshBar() → renders 10 buttons
  │       ├─ Building cards → BuildBuildingCommand + placement mode
  │       └─ Other cards → PlayCardCommand → CardDeckController.PlayCard()
  │
  ├─ CardDeckManager (if DeckSO assigned) → DrawHand() → OnHandChanged
  │   └─ CardDeckUI renders hand + CardUI per card
  │
  ├─ AbilityHandUI → collects ActiveAbilityCommands from buildings
  │
  └─ SectorManager.OnSectorUnlocked → CardDeckController.TriggerDraft()
```

---

## Editor Tools

| File | Purpose |
|---|---|
| `Assets/Editor/CardDeckSetup.cs` | Menu items under `Tools/Card Deck/` — `Create Card Assets from Unlockables` (creates CardSO assets from existing UnlockableSOs), `Setup Main Deck` (populates MainDeck.asset with all CardSOs) |

---

## Version History
- 2026-06-29: Initial implementation of health bar system
- 2026-06-29: Visual script version created for easier maintenance
- 2026-06-30: Fixed SurvivalManager overwriting DecayStarter integrity writes; switched to drain-based approach
- 2026-07-08: Implemented dual card system architecture:
  - v2 Tech Tree cards (`CardSO`/`CardDeckSO`/`CardDeckManager`) with weighted draws, rarity, play costs, direct supply effects
  - Original BlueprintCardSO hierarchy (`UnlockBuildingCardSO`, `SpawnUnitCardSO`, `ResourceShipmentCardSO`, `PassiveBuffCardSO`, `DiscoveryCardSO`, `ScoutingCardSO`, `DrillBreakthroughCardSO`, `TerraformingCardSO`) with single `Apply()` pattern
  - CardDeckController with 10-card hand, 3 guaranteed starters, play-and-draw replacement
  - BlueprintDraftManager static unlocking system with buff multipliers
  - BottomBarActionsUI now renders hand cards with placement mode for buildings
  - AbilityHandUI for building active abilities with cooldown display
  - BlueprintDraftUI for generation-start card drafting
  - CardDeckSetup editor tools for batch card creation
  - 28+ card asset files in `Assets/Resources/Cards/`