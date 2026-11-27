# Design: MainGame Survey Team card

**Date:** 2026-11-19  
**Status:** Approved  
**Engine:** Unity MainGame

## Summary

Replace free-flying single-drone survey with a **Survey Team** card:

1. Play card → paint up to **N tiles** from lit ground  
2. Click green **✓** on last tile  
3. Search takes **D colony days** on the time-lapse clock  
4. Drones + crew animate over the painted area while days tick  
5. On complete: tiles stay searched (light tint); deposits/`?` under them uncover  

## Defaults (v1)

| Field | Value |
|-------|------:|
| Max paint tiles | 5 |
| Search duration | 3 colony days |
| Min tiles for ✓ | 2 |

**Over budget:** The card includes **5** painted hexes. Each **additional** hex costs **colony days** on the time-lapse clock (default **1 day**/hex). Completed research reduces that rate (`MainGameResearch.SurveyOverageDaysPerHex`; **Extended Survey** −25% multiplier). Full weeks still tick when day debt crosses a week boundary. Player confirms with ✓ when ready; no auto-send at 5.

Colony day = `MainGameTimeLapse.SecondsPerWeekNormal / 7` at 1× (weeks still end on full week; search is independent day accum).

## Card

- Reuse `SurveyDroneCard` / `ScoutingCardSO.SurveyDrone` in MainGame  
- UI copy: “Survey Team — paint up to 5 tiles, then ✓. Takes 3 days.”  
- Playing the card enters paint mode; card consumes when ✓ confirms (not on Apply alone if paint cancelled)

## Flow

1. Objective wants survey → hand seats Survey card  
2. Play card → `BeginSurveyMission(maxTiles, days)`  
3. Paint adjacent trail (existing refusal rules), ✓ when ≥2  
4. Confirm → start mission; hide paint marks; spawn search team anim on path  
5. While mission active: accumulate scaled time as days; team wanders path  
6. Mission complete → `RevealTiles(path)` + resource fog reveal; unlock Aquifer if ice found  

## Tone

- Survey is part of a **Frostpunk 2–like** (not casual) colony pressure fantasy.
- Exploration should feel like a **spatial puzzle** (Blue Prince–like): limited paint budget, day cost, and committing to a route toward `?`s — not free fog-clear micro.

## Art notes

- Aerial units = survey drones (keep the small procedural Survey Team look).
- Ground **capsules = spacesuit people** (suited colonists / crew). Capsule mesh is a **placeholder** until proper suited-character art exists — do not reinterpret them as drones or props.

## Out of scope

- Multiple concurrent survey missions  
- Steel cost / Unreal rover weeks fidelity  
- Final character art (procedural drones/crew placeholders OK)  
