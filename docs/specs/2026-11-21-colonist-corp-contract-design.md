# Design: Colonists terraform, the corporation holds the contract

**Date:** 2026-11-21  
**Status:** In the game  
**Engine:** Unity MainGame  
**Builds on:** the MainGame run in [`PROJECT_KNOWLEDGE.md`](../../PROJECT_KNOWLEDGE.md) (cards on tiles, End Week, shuttle, storm).

## 1. Player fantasy

You play cards on tiles. A card points the colonists at a job. They build it, and they are the ones who terraform. They keep working only while you meet the needs that job requires.

The corporation holds the contract. It has a clock, and it can cancel if the colonists’ satisfaction falls too far, or if it loses faith that you will finish the mission. The fun is aiming the colony and supplying it so the colonists hit the terraforming goal before that happens.

## 2. Decision

1. Card play stays. The player still chooses the card and the tile, under the placement rules that already exist (survey paint, ice range, deposit ghosts, scaffolding, mineral cost, haul weeks).
2. Colonists perform the terraforming that card named. Construction weeks and weekly air, ice, and mine output are their work.
3. The corporation is simulated as the contract holder: deadlines, satisfaction, and faith. It cancels the contract when any one of those fails.
4. The terraforming number moves on a week only when the colonists actually ran the job. A card on a pad with unmet needs sits still.
5. This layer lands in the MainGame first. Colony Acts stays archived.

## 3. Three actors

| Actor | What they control |
| --- | --- |
| Player | Which card, which tile, and whether the stocks, power, beds, and air the job needs are on hand |
| Colonists | Building the aimed job over its weeks, then running it so the planet number changes |
| Corporation | The contract clock, colonist satisfaction, and its faith in the player |

A card is a nudge with a place. It tells the colonists which job to run. It does not skip their needs, and it does not move the goal by itself.

## 4. What the player still does

Unchanged from the current MainGame:

- Survey Team: paint up to 5 tiles, confirm, 3 colony days, then reveal.
- Place Ice Aquifer within 3 of revealed ice.
- Place Oxygen Processor, habitats, the second drill, and the Geothermal Generator on legal tiles.
- Every building is built with scaffolding on the pad. Nothing rises out of the ground.
- End Week advances the colony clock.
- Mines are placed on the matching deposit ghost and spend the mine mineral stock.

Playing the card assigns the job. The weeks after that are the colonists carrying it out.

## 5. What the colonists do

On each End Week, colonists work jobs the player has already aimed.

**Construction.** A site on the board advances one week when colonist satisfaction at the start of the week is **40 or above**. Below 40, the site stays where it is and the week log says the crew stopped. Haul weeks and build weeks are otherwise the rules already in `MainGameColony`.

**Terraforming.** A finished Oxygen Processor adds its weekly air only when all of these are true:

- Satisfaction at the start of the week is **40 or above**
- The processor is powered
- Ice stock covers `IceConsumedPerWeek`

An aquifer pulls ice, and a mine credits its stock, under that same satisfaction gate. Below 40, those yields do not happen and the log names the stall.

**Needs that move satisfaction.** Evaluated after the week’s work, in this order. Apply every matching line.

| Week result | Satisfaction |
| --- | ---: |
| Start of a new run | 80 |
| Air was critical (colonists breathed less than `O2Need`) | −20 |
| Shuttle has landed and beds are fewer than colonists | −15 |
| No critical air, and beds cover colonists once the shuttle has landed | +8 |
| Cap | 0 to 100 |

Starting crew is `StartColonists` (12). The shuttle adds `MainGameShuttleSize` (6). Air need stays `ceil(colonists × 0.5)`.

Satisfaction is the colonists’ willingness to work. At 40 and above they build and terraform. Below 40 they are still on the pad, and the job waits.

## 6. What the corporation does

The corporation does not place buildings. It watches the contract.

**Faith** starts at **80**. After satisfaction is updated for the week:

| Week result | Faith |
| --- | ---: |
| The prologue step advanced this week | +15 |
| The step did not advance | −8 |
| Shuttle is scheduled, 2 or fewer weeks remain, and beds are under 20 | −15 more |
| Storm is scheduled, 2 or fewer weeks remain, and the Geothermal Generator is not finished beside the oxygen processor | −15 more |
| Cap | 0 to 100 |

The step check uses the step value from before `StoryCheck` runs. A week that finishes the current objective is a faith gain. A quiet week is a loss of faith.

**Clock.** The corporation’s deadlines are the ones the MainGame already schedules:

- Shuttle: `MainGameShuttleWeeks` (3) after the first powered oxygen processor.
- Storm: `MainGameStormWeeks` (2) after the second ice drill, then the colony must still have oxygen above 0 for `MainGameStormWeeks` more weeks to clear the prologue.

No separate week cap is added. A colony that never reaches those deadlines still loses, because faith falls on every week the step does not advance.

## 7. How a week ends

`MainGameColony.EndWeek` keeps its current order, with the contract wrapped around it.

1. Read satisfaction. If it is below 40, skip site progress and skip aquifer, mine, and oxygen yields. Log the stall.
2. Run the rest of the week as it does now (breathe, shuttle landing, storm flag, week increment).
3. Apply the satisfaction table, then the faith table.
4. If satisfaction is 0 or faith is 0, the corporation cancels the contract. Do not advance the story to a win on that same week.
5. Otherwise run `StoryCheck`.
6. If the dust storm is active and oxygen is 0 after breathing, the contract fails on the clock: the mission was not held.

A cancel ends the run. The clock stays stopped, Space and the speed buttons cannot resume it, and cards and End Week do nothing. Auditor Kline’s cutscene says the mission is over. New game reloads the scene.

Cancel copy:

- Satisfaction at 0: “The corporation cancelled the contract. Colonist satisfaction is too low.”
- Faith at 0: “The corporation lost faith in this mission and cancelled the contract.”
- Storm and oxygen at 0: “The corporation cancelled the contract. The mission ran out of air.”

Clearing step 6 (`Oxygen > 0` through the storm hold) is still the win, and it happens only when step 4 did not already cancel.

## 8. How the player sees it

The prologue HUD keeps the objective line and the air readout. It adds three facts:

- **Satisfaction** — 0 to 100, and whether the crew is working (40 or above) or stopped.
- **Faith** — 0 to 100.
- **Next deadline** — weeks until the shuttle, or weeks until the storm, once that deadline exists. Before either is scheduled, the line says the corporation is waiting on the current objective.

The End Week log states the colonist result in one line: air gained, crew stopped, or a need that failed (no ice, no power, not enough beds, air critical). The goal number changing is the proof the colonists did the work. A card that only sits on the pad is a nudge that has not landed yet.

## 9. Scope

### In

- Satisfaction and faith on `MainGameColony`, shown on `MainGameHUD`, updated only from `EndWeek`.
- Work gate at satisfaction 40 for construction progress and for ice, mine, and oxygen yields.
- Contract loss on satisfaction 0, faith 0, or storm with no air.
- Week log lines for stall, cancel, and terraforming output.

### Out

- Colonists choosing the building or the tile.
- Random arrivals, skills, or hiring.
- Removing card placement, scaffolding, survey paint, or deposit ghosts.
- A new global week limit besides the shuttle and storm clocks.
- Colony Acts quotas, Terra-Coins, or adjacency scores.
- Individual colonist agents walking the map. The survey crew and harvest drones stay hidden. The simulation is the weekly result, not a crowd.

## 10. First numbers

These are the values to ship. Tune them in `MainGameData` if a full prologue run cancels while the existing step goals are being met on pace, or if faith never falls on a stalled run.

| Constant | Value |
| --- | ---: |
| Satisfaction start | 80 |
| Faith start | 80 |
| Work threshold | 40 |
| Air-critical satisfaction loss | 20 |
| Unhoused satisfaction loss | 15 |
| Satisfaction gain when air is met and, after the shuttle, beds cover colonists | 8 |
| Step-advanced faith gain | 15 |
| Quiet-week faith loss | 8 |
| Deadline-pressure faith loss | 15 |
| Deadline-pressure window | 2 weeks |

## 11. Tests

- Satisfaction 39: an in-progress site does not gain a week, and a powered oxygen processor with ice does not add air.
- Satisfaction 40: that site gains a week, and the processor adds `OxygenProducedPerWeek`.
- A week with critical air reduces satisfaction by 20. A following week with air covered and beds covered gains 8.
- A week that does not change `MainGameStep` reduces faith by 8. A week that advances the step gains 15.
- Satisfaction 0 or faith 0 ends the run and does not set step 6.
- Storm active and oxygen 0 ends the run.
- Step 6 still requires oxygen above 0 for the storm hold, with satisfaction and faith above 0 on that week.
