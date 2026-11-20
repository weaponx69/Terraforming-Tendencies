# Prologue time-lapse (Frostpunk-style)

**Approved:** default **paused**; Space toggles pause; speed buttons on Prologue banner. Hit 1x to start weeks.

## Behavior
- Colony weeks auto-advance on a real-time clock (`PrologueTimeLapse`).
- Does **not** set `Time.timeScale` — camera / drone / UI stay snappy.
- Speeds: Pause · Slow (0.5×) · Normal (1×, ~20s/week) · Fast (2×) · Faster (4×).
- Banner buttons use ASCII (`II` `0.5x` `1x` `2x` `4x`) so TMP fonts always show them.
- **End Week** still available as a manual skip; resets the week progress bar.
- Spacebar toggles pause ↔ last non-pause speed.

## UI
Banner controls: `❚❚` `½` `▶` `▶▶` `▶▶▶` + week progress fill + speed label.
