# Card Deck Redesign Plan

## Current Problem

`CardDeckController.DrawCard()` filters OUT cards for already-unlocked buildings. Since `BlueprintDraftManager` unlocks most buildings at startup, there are effectively no valid cards to draw.

## What the User Wants

1. **All actionables** are in the deck from the start (building blueprints, unit training cards, resource cards, etc.)
2. **Starting hand of 5 cards**: Command Post, Mining Drone, Solar Panel (guaranteed) + 2 random
3. **Play-and-draw**: When a card is played, draw a random replacement from the deck
4. **Cards are consumable** — using a card removes it from your hand and you draw a new one

## Architecture

### Cards Are Actions, Not Unlockables
The shift is: cards no longer "unlock" a building — they **directly represent the action**. Playing a "Build Command Post" card means you can build one Command Post right now. After use, the card is discarded and you draw a new one.

### Starting Hand
- **3 guaranteed**: Command Post, Mining Drone, Solar Panel
- **2 random**: Drawn from the deck

### Draw Logic
- When a card is played → draw a random card from the deck
- If deck is empty, reshuffle discard pile
- If no cards remain at all, stop drawing

## Files to Create/Modify

### Modify `CardDeckController.cs`
- Change `DrawCard()` to add to hand instead of auto-applying
- Add `hand` (List<BlueprintCardSO>) — the player's current hand
- Add `DrawToHand()` — draws a card from drawPile into the hand
- Starting hand setup: guarantee Command Post, Mining Drone, Solar Panel + 2 random
- Remove filtering of already-unlocked buildings from DrawCard

### Modify `BlueprintDraftManager.cs`
- Start with NO unlocked buildings (or minimal set)
- Unlock buildings dynamically as cards are drafted/drawn

### Modify UI (`BlueprintDraftUI` or card hand UI)
- Show the hand of 5 cards persistently
- When a card is clicked, play it and draw replacement

## Implementation Order
1. Fix `CardDeckController.DrawCard()` to add to hand instead of filtering
2. Set up starting hand logic
3. Wire up play-and-draw flow
4. Update UI to show hand