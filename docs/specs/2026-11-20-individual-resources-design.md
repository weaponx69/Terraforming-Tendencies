# Design: Individual resources (not Materials collapse)

**Date:** 2026-11-20  
**Status:** Implementing (Phase 1)  
**Engine:** Unity MainGame first; Acts Materials bank is archived for the MainGame run

## Summary

Deposit types stay **individual currencies**. Uncovering a `?` does not dump into a shared Materials pile. The player places a **mine building on the deposit**; production ticks on the time-lapse clock while Survey-style drones sell the harvest visually.

## Resource types (catalog)

Possible deposit / stock types (not every planet has all of them):

| Deposit / stock | Typical mine (existing cards / stand-ins) | Notes |
|-----------------|------------------------------------------|--------|
| Ice             | Water Ice Aquifer                        | Life support (Ice Stock → Oxygen) when the planet supports ice |
| Minerals        | Deep-Core Mining Laser                   | Separate stock |
| Gas             | Gas Well (O₂ Processor mesh stand-in)    | Separate stock; place on Gas pad |
| Iron            | Iron Ore Extractor (Foundry mesh)        | Separate stock; place on Iron pad |
| Regolith        | Basalt Strip-Mine                        | Separate stock |

**Retired for MainGame:** collapsing mine output into one **Materials** bank (Acts-era).

## Planet type drives which resources exist

- **Not every planet has the same resource mix.** Availability is defined by **planet type** / `PlanetConfig` (and related generation), not a fixed global set.
- Generation only scatters deposit types allowed for that planet; Survey `?` marks and mine cards only appear for types that exist here.
- HUD / hand only surface stocks and extractors for types present (or already unlocked) on the current planet.
- Ice may be rare or absent on some worlds — MainGame oxygen pressure must respect that planet’s rules (alternate life-support path is out of scope for this doc unless specified later).
- **Future:** pre-run **planet select** UI — player picks the world type to colonize; that choice feeds `PlanetConfig` (resources + pressure). Not required for the current MainGame slice.

## Player flow

1. Survey Team uncovers a typed deposit (`?` → label + pad).  
2. Matching mine unlock/card becomes placeable **only on that deposit tile**.  
3. On place: building registers as extractor for that type.  
4. Each colony week (or day, if we align with ice): building adds to that type’s stock and depletes the deposit.  
5. Visual: 1–2 small Survey-style drones loop the pad (same family as Survey Team aerials) — **not** a selected Mining Drone unit.

## HUD

- Show per-type counts (Ice already on AIR strip; add Minerals / Gas / Iron / Regolith as needed when first unlocked).  
- Do not reintroduce a MainGame “Materials” total that sums them.

## Survey Team art (related)

- Search-mission ground capsules = **spacesuit people** placeholders (see Survey Team design).

## Out of scope (v1)

- Full crafting recipes that spend multiple types  
- Player-commanded Mining Drone gather micro  
- Converting old Acts Materials quota win condition  
