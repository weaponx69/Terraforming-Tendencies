# Hand-Based Bottom Bar Plan

## Goal
The bottom bar always shows exactly 5 action cards. Using a card removes it and draws a replacement from the deck.

## Changes

### 1. `BottomBarActionsUI.cs` — Hand Management
- Wire exactly 5 buttons (down from 12)
- Maintain a `hand` list of 5 `BaseCommand` instances
- When a command is used (handled via `CommandSelectedEvent`), remove it from hand and call `DrawCard()` to replace it
- Initial hand: Command Post, Mining Drone, Solar Panel + 2 random cards drawn from deck
- Cards that are "used up" (consumed) are removed from hand and replaced
- If deck is empty, that slot stays empty

### 2. `CardDeckController.cs` — Draw to Hand
- Keep the draw pile logic
- `DrawCard()` now returns a `BlueprintCardSO` instead of auto-applying it
- The caller (BottomBarActionsUI) decides when to apply the card
- OR: `DrawCard()` draws and auto-applies, and the bottom bar just rebuilds from unlocked buildings

### 3. Approach Comparison
| Approach | Complexity | Pros | Cons |
|---|---|---|---|
| **A: Hand-based** (5 explicit card slots) | Higher | Visible card management, clear UX | Big refactor of BottomBarActionsUI |
| **B: Mark certain cards as renewable** | Lower | Solar Panel always available | Doesn't solve the "hidden draw" problem |
| **C: Log what DrawCard() does** | Lowest | See if draws are happening | No gameplay change |

## Recommended
Start with **Option C** — add a console log to see what `DrawCard()` is actually drawing. If draws are working but just invisible, the user may just need visual feedback. If draws aren't working, we investigate why.