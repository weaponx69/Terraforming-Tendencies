using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Frostpunk-style MainGame time-lapse. Speed buttons drive <see cref="Time.timeScale"/>
    /// so animations, building, movement, and week ticks all share one clock.
    /// Default: <b>paused</b> (timeScale 0). Space toggles pause.
    /// </summary>
    public class MainGameTimeLapse : MonoBehaviour
    {
        public static MainGameTimeLapse Instance { get; private set; }

        public enum Speed
        {
            Pause = 0,
            Slow = 1,
            Normal = 2,
            Fast = 3,
            Faster = 4
        }

        /// <summary>Scaled seconds per colony week at Normal (1×).</summary>
        public const float SecondsPerWeekNormal = 20f;

        public Speed CurrentSpeed { get; private set; } = Speed.Pause;
        public float WeekProgress01 { get; private set; }

        Speed speedBeforePause = Speed.Normal;
        Speed speedBeforeBriefing = Speed.Pause;
        float weekAccum;
        bool spaceLatch;
        bool appliedScale;
        bool briefingHold;
        bool runEnded;

        /// <summary>The corporation cancelled the contract. The clock stays stopped.</summary>
        public bool RunEnded => runEnded;

        public static float MultiplierFor(Speed speed) => speed switch
        {
            Speed.Pause => 0f,
            Speed.Slow => 0.5f,
            Speed.Normal => 1f,
            Speed.Fast => 2f,
            Speed.Faster => 4f,
            _ => 1f
        };

        public bool IsPaused => CurrentSpeed == Speed.Pause;

        /// <summary>A crisis cutscene holds the clock until the player dismisses it.</summary>
        public void HoldForBriefing(bool hold)
        {
            if (runEnded)
            {
                CurrentSpeed = Speed.Pause;
                ApplyTimeScale();
                return;
            }

            if (hold)
            {
                if (briefingHold) return;
                speedBeforeBriefing = CurrentSpeed;
                briefingHold = true;
                CurrentSpeed = Speed.Pause;
                ApplyTimeScale();
                return;
            }

            ReleaseBriefingHold();
        }

        /// <summary>Dismiss weekly report / opening briefing — clock runs again (default 1× if it was held on pause).</summary>
        public void ReleaseBriefingHold()
        {
            if (!briefingHold || runEnded) return;
            briefingHold = false;
            Speed resume = speedBeforeBriefing != Speed.Pause
                ? speedBeforeBriefing
                : speedBeforePause != Speed.Pause ? speedBeforePause : Speed.Normal;
            SetSpeed(resume);
        }

        /// <summary>Contract cancelled. Pause stays on; speed and Space cannot resume the run.</summary>
        public void EndRun()
        {
            runEnded = true;
            briefingHold = true;
            CurrentSpeed = Speed.Pause;
            ApplyTimeScale();
            GameDevTV.RTS.UI.MainGameHUD.Instance?.RefreshTimeControls();
        }

        void Awake()
        {
            Instance = this;
            CurrentSpeed = Speed.Pause;
            speedBeforePause = Speed.Normal;
            ApplyTimeScale();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Don't leave the Editor/Play session frozen if this object dies while paused.
            if (appliedScale && Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;
        }

        void OnDisable()
        {
            if (appliedScale && IsPaused)
                Time.timeScale = 1f;
        }

        void Update()
        {
            if (!MainGameController.IsActive) return;

            HandleSpaceToggle();

            float mult = MultiplierFor(CurrentSpeed);
            if (mult <= 0f)
            {
                WeekProgress01 = Mathf.Clamp01(weekAccum / SecondsPerWeekNormal);
                return;
            }

            // Survey Team days tick independently in MainGameSurveyMission; weeks keep advancing.

            // Time.deltaTime already includes timeScale, so Slow/Fast match animations & builds.
            weekAccum += Time.deltaTime;
            WeekProgress01 = Mathf.Clamp01(weekAccum / SecondsPerWeekNormal);

            int weekSafety = 0;
            var weekSw = Stopwatch.StartNew();
            while (weekAccum >= SecondsPerWeekNormal && weekSafety < 50)
            {
                weekAccum -= SecondsPerWeekNormal;
                weekSafety++;
                MainGameController.Instance?.EndWeek();
            }

            weekSw.Stop();
            if (weekSw.Elapsed.TotalMilliseconds > 2.0)
                Debug.LogError(
                    $"[MainGameTimeLapse] EndWeek loop exceeded 2ms: {weekSw.Elapsed.TotalMilliseconds:F2}ms " +
                    $"iterations={weekSafety} weekAccum={weekAccum:F3}");

            if (weekAccum < SecondsPerWeekNormal)
                WeekProgress01 = Mathf.Clamp01(weekAccum / SecondsPerWeekNormal);

            if (mult > 0f)
            {
                MainGameConstruction.RequestTimeLapseTick();
                MainGameConstruction.TickAllFromTimeLapse();
            }
        }

        void HandleSpaceToggle()
        {
            if (Keyboard.current == null) return;
            bool down = Keyboard.current.spaceKey.wasPressedThisFrame;
            if (!down)
            {
                spaceLatch = false;
                return;
            }
            if (spaceLatch || briefingHold) return;
            spaceLatch = true;
            TogglePause();
        }

        public void TogglePause()
        {
            if (runEnded) return;
            if (CurrentSpeed == Speed.Pause)
                SetSpeed(speedBeforePause == Speed.Pause ? Speed.Normal : speedBeforePause);
            else
            {
                speedBeforePause = CurrentSpeed;
                SetSpeed(Speed.Pause);
            }
            GameDevTV.RTS.UI.MainGameHUD.Instance?.RefreshTimeControls();
        }

        public void SetSpeed(Speed speed)
        {
            if (runEnded) return;
            if (speed != Speed.Pause)
                speedBeforePause = speed;
            CurrentSpeed = speed;
            ApplyTimeScale();
            GameDevTV.RTS.UI.MainGameHUD.Instance?.RefreshTimeControls();
        }

        /// <summary>After Survey ✓ — days should tick without an extra 1x click.</summary>
        public void ResumeAfterSurveyConfirm()
        {
            if (runEnded || briefingHold) return;
            if (CurrentSpeed != Speed.Pause) return;
            SetSpeed(speedBeforePause == Speed.Pause ? Speed.Normal : speedBeforePause);
        }

        void ApplyTimeScale()
        {
            Time.timeScale = MultiplierFor(CurrentSpeed);
            appliedScale = true;
        }

        /// <summary>Called when the player presses End Week — reset the auto clock so they don't double-tick.</summary>
        public void NotifyManualEndWeek()
        {
            weekAccum = 0f;
            WeekProgress01 = 0f;
        }

        /// <summary>Survey paint overage — advance colony clock by whole or fractional days (may trigger End Week).</summary>
        public void AdvanceColonyDays(float days, int maxEndWeeksThisCall = 24)
        {
            if (runEnded || !MainGameController.IsActive || days <= 0.0001f) return;
            float daySec = SecondsPerWeekNormal / 7f;
            weekAccum += daySec * days;
            int safety = 0;
            int cap = Mathf.Clamp(maxEndWeeksThisCall, 1, 24);
            while (weekAccum >= SecondsPerWeekNormal && safety < cap)
            {
                weekAccum -= SecondsPerWeekNormal;
                safety++;
                MainGameController.Instance?.EndWeek();
            }

            if (weekAccum < SecondsPerWeekNormal)
                WeekProgress01 = Mathf.Clamp01(weekAccum / SecondsPerWeekNormal);
            GameDevTV.RTS.UI.MainGameHUD.Instance?.RefreshTimeControls();
        }

        public string SpeedLabel()
        {
            if (runEnded) return "CONTRACT CANCELLED";
            return CurrentSpeed switch
            {
                Speed.Pause => "PAUSED (Space to resume)",
                Speed.Slow => "0.5x Slow",
                Speed.Normal => "1x Normal",
                Speed.Fast => "2x Fast",
                Speed.Faster => "4x Faster",
                _ => "1x"
            };
        }
    }
}
