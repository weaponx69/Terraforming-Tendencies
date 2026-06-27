# Card Deck System — Architecture Plan (v2: Cards Are Core Gameplay)

## Design Pivot

The player's ability to **draw and play cards is the primary survival mechanic**. Milestone progress is directly driven by card play, not just by passive building construction. The player must strategically draw cards to get the buildings/resources they need, and play them at the right time to complete milestones before environmental conditions become fatal.

---

## Core Loop

```
┌─────────────────────────────────────────────────────────┐
│  GENERATION N                                            │
│                                                          │
│  1. Draw Phase: Player spends Materials to draw cards    │
│  2. Play Phase: Cards in hand can be played for cost     │
│     → Playing a card triggers its UnlockableSO effect    │
│     → Effects directly impact milestone progress          │
│  3. Milestone Check: Did the player hit 100%?            │
│     YES → Generation complete, advance to next           │
│     NO  → Draw more cards or wait for resource income    │
│                                                          │
│  FAILURE: If milestone timer runs out and progress < 100%│
│     → Colony lost, game over                             │
└─────────────────────────────────────────────────────────┘
```

---

## How Cards Drive Milestone Progress

Each `CardSO` wraps an `UnlockableSO`. When played, the card's effect is applied through the existing `UpgradeResearchedEvent` pipeline. The key addition is that **playing a card also directly modifies milestone-relevant supplies**:

| Card Type | Example | What Happens When Played |
|---|---|---|
| **Biomass Card** | "Plant Dome" | Fires unlock event + directly increases `Supplies.Biomass` |
| **Oxygen Card** | "Oxygen Processor" | Fires unlock event + directly increases `Supplies.Oxygen` |
| **Power Card** | "Solar Panel" | Fires unlock event + directly increases `Supplies.Power` |
| **Population Card** | "Habitat" | Fires unlock event + directly increases `Supplies.Population` |
| **Command Card** | "Command Post" | Fires unlock event + directly counts toward CommandPosts milestone |
| **Resource Card** | "Mining Drone" | Fires unlock event + grants Materials |
| **Environment Card** | "Heating Array" | Fires unlock event + increases Temperature/Atmosphere/Water |

The `CardSO` has an optional `DirectEffect` field that specifies which supply to modify and by how much. This is in addition to the unlock event — so a card both unlocks the tech tree entry AND gives an immediate supply boost.

---

## New Files to Create

### 1. `Assets/Scripts/TechTree/CardRarity.cs`
```csharp
namespace GameDevTV.RTS.TechTree
{
    public enum CardRarity { Common, Uncommon, Rare, Epic }
}
```

### 2. `Assets/Scripts/TechTree/CardEffectType.cs`
```csharp
namespace GameDevTV.RTS.TechTree
{
    public enum CardEffectType
    {
        None,
        Biomass,
        Oxygen,
        Power,
        Population,
        Materials,
        Temperature,
        Atmosphere,
        Water,
        CommandPost
    }
}
```

### 3. `Assets/Scripts/TechTree/CardSO.cs` — Core card data

| Field | Type | Purpose |
|---|---|---|
| `Name` | `string` | Display name |
| `Icon` | `Sprite` | Card art |
| `WrappedUnlockable` | `UnlockableSO` | The unlockable this card represents |
| `Rarity` | `CardRarity` | Affects draw weight |
| `DrawWeight` | `float` | Higher = more likely drawn (default 1.0) |
| `PlayCost` | `int` | Materials cost to play this card |
| `EffectType` | `CardEffectType` | Which supply the card modifies |
| `EffectAmount` | `float` | How much to modify (e.g., +15 Biomass) |
| `Description` | `string` | Flavor text |

### 4. `Assets/Scripts/TechTree/CardDeckSO.cs` — Deck configuration

| Field | Type | Purpose |
|---|---|---|
| `AllCards` | `List<CardSO>` | Master list of all cards |
| `HandSize` | `int` | Cards drawn per hand (default 5) |
| `MaxHandSize` | `int` | Maximum cards in hand (default 7) |
| `DrawCost` | `int` | Materials cost to draw a new hand (default 50) |
| `RefreshOnNewGeneration` | `bool` | Auto-refresh deck on generation start |

**Key method: `BuildDrawPool(Owner owner)`**
- Iterates `AllCards`
- Filters to cards where `WrappedUnlockable.TechTree.IsUnlocked(owner, unlockable)` is true
- Filters to cards where `!IsResearched(owner, unlockable)` (not yet unlocked)
- Returns `List<CardSO>` — the draw pool

### 5. `Assets/Scripts/Player/CardDeckManager.cs` — Runtime singleton

| Field | Type | Purpose |
|---|---|---|
| `DeckSO` | `CardDeckSO` | Deck configuration |
| `DrawPool` | `List<CardSO>` | Current draw pool |
| `Hand` | `List<CardSO>` | Cards currently in hand |
| `DiscardPile` | `List<CardSO>` | Played/discarded cards |

**Key methods:**
- `BuildDrawPool()` — calls `DeckSO.BuildDeck(Owner)`, stores result
- `DrawHand()` — draws `HandSize` cards using weighted random, costs `DrawCost` Materials
- `PlayCard(CardSO card)` — checks `CanPlayCard`, deducts `PlayCost`, fires `UpgradeResearchedEvent`, applies `DirectEffect`, moves to discard
- `CanPlayCard(CardSO card)` — returns (IsUnlocked AND HasEnoughSupplies)
- `RefreshDeck()` — rebuilds draw pool, draws new hand
- `ApplyCardEffect(CardSO card)` — modifies `Supplies` based on `EffectType`/`EffectAmount`

**Events:**
- `OnHandChanged(List<CardSO> hand)` — fired when hand changes
- `OnCardPlayed(CardSO card)` — fired on successful play
- `OnDeckRefreshed()` — fired when deck is rebuilt
- `OnGenerationStarted(int gen, int max)` — subscribe to `GenerationManager.OnGenerationStarted`

### 6. `Assets/Scripts/UI/Containers/CardDeckUI.cs` — Hand UI

| Field | Type | Purpose |
|---|---|---|
| `CardPrefab` | `GameObject` | Card UI prefab |
| `HandContainer` | `Transform` | Parent for card objects |
| `DrawButton` | `Button` | Draw new hand button |
| `DrawCostText` | `TextMeshProUGUI` | Shows draw cost |
| `MaterialText` | `TextMeshProUGUI` | Shows player's Materials |

### 7. `Assets/Scripts/UI/Components/CardUI.cs` — Individual card

Shows icon, name, rarity color border, play cost, effect description. Button calls `CardDeckManager.PlayCard(card)`. Button is disabled if `!CanPlayCard(card)`.

---

## Integration with Existing Systems

| Existing System | How It's Used |
|---|---|
| `TechTreeSO.IsUnlocked()` | Determines draw pool eligibility |
| `TechTreeSO.IsResearched()` | Filters out already-unlocked cards |
| `Supplies.Materials` | Draw cost + play cost |
| `Supplies.Biomass/Oxygen/Power/Population/etc.` | Modified by card `DirectEffect` |
| `UpgradeResearchedEvent` | Fired on card play to trigger unlock |
| `GenerationManager.OnGenerationStarted` | Refreshes deck, draws starting hand |
| `GenerationManager.OnGenerationEnded` | Clears hand, discards to pile |
| `GenerationManager.Update()` | Milestone progress now includes card-applied supply boosts |

---

## How the Player Completes a Round (v2)

```
Generation N starts
  │
  ├─ CardDeckManager.OnGenerationStarted → RefreshDeck() + DrawHand()
  │
  ├─ Player sees hand of 5 cards
  │   ├─ Play "Solar Panel" (-50 Materials → +25 Power, unlocks tech)
  │   ├─ Play "Plant Dome" (-30 Materials → +15 Biomass, unlocks tech)
  │   ├─ Draw new hand (-50 Materials → 5 new cards)
  │   └─ Play "Oxygen Processor" (-40 Materials → +20 Oxygen, unlocks tech)
  │
  ├─ GenerationManager.Update() checks milestone progress
  │   └─ Progress = min(primary, temp, atmos, water)
  │       └─ Card-applied boosts directly improve these values
  │
  ├─ Progress >= 100% → TriggerGenerationEnd()
  │   └─ Liquidate remaining Materials → Terra-Coins
  │   └─ Show GenerationSummaryUI
  │   └─ Player clicks StartNextGeneration()
  │
  └─ Progress < 100% and time runs out → Game Over
```

---

## Visual Scripting Integration

### `CardDeckManager` — `[Inspectable]` + Flow Graph candidate
- **Inspectable:** `DeckSO`, `DrawPool`, `Hand`, `DiscardPile`
- **Flow Graph inputs:** `DrawHand()`, `PlayCard(CardSO)`, `RefreshDeck()`
- **Flow Graph outputs:** `OnHandChanged`, `OnCardPlayed`, `OnDeckRefreshed`
- A Flow Graph on this component wires UI updates when hand changes

### `CardSO` — `[Inspectable]` candidate
- All serialized fields visible in VS graphs
- Useful for "create card" editor workflows

### `CardDeckSO` — `[Inspectable]` candidate
- `AllCards`, `HandSize`, `DrawCost` visible for editor configuration

---

## Implementation Order

| Step | File | Description |
|---|---|---|
| 1 | `CardRarity.cs` | Rarity enum |
| 2 | `CardEffectType.cs` | Effect type enum |
| 3 | `CardSO.cs` | Card data definition with `[Inspectable]` |
| 4 | `CardDeckSO.cs` | Deck configuration with `[Inspectable]` |
| 5 | `CardDeckManager.cs` | Runtime manager with `[Inspectable]` |
| 6 | `CardUI.cs` | Individual card UI component |
| 7 | `CardDeckUI.cs` | Hand container UI |
| 8 | Create card `.asset` files | Populate deck from existing UnlockableSOs |
| 9 | Wire Flow Graph on CardDeckManager | Connect events to UI |
| 10 | Add CardDeckUI to scene | Place in UI canvas |
| 11 | Hook into GenerationManager | Subscribe to generation events |
| 12 | Verify build and test | Play Mode verification |

---

## Key Design Decisions

1. **Cards are the primary progression path.** Building construction through normal means still exists, but cards are the reliable, strategic way to hit milestone targets. Random card draws create tension — you might not get the card you need.

2. **Direct effects are immediate.** When you play a "Solar Panel" card, you get +25 Power instantly. No build time, no construction. This makes card play feel impactful and distinct from normal building.

3. **Draw cost creates strategic tension.** Drawing costs Materials. Playing costs Materials. The player must decide: draw hoping for better cards, or play what they have? Running out of Materials means you can't draw AND can't play — a death spiral.

4. **The tech tree gates the pool.** Cards for unlockables the player hasn't researched yet don't appear in the draw pool. This prevents the frustration of drawing cards you can never play.

5. **No existing files are modified.** All new functionality is in new files. The `[Inspectable]` attributes are only on new types. The existing `GenerationManager` milestone logic continues to work — card effects just boost the supply values it reads.
