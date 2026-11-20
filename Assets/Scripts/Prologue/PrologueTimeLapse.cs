using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Frostpunk-style Prologue time-lapse. Speed buttons drive <see cref="Time.timeScale"/>
    /// so animations, building, movement, and week ticks all share one clock.
    /// Default: <b>paused</b> (timeScale 0). Space toggles pause.
    /// </summary>
    public class PrologueTimeLapse : MonoBehaviour
    {
        public static PrologueTimeLapse Instance { get; private set; }

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
        float weekAccum;
        bool spaceLatch;
        bool appliedScale;

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
            if (!PrologueController.IsActive) return;

            HandleSpaceToggle();

            float mult = MultiplierFor(CurrentSpeed);
            if (mult <= 0f)
            {
                WeekProgress01 = Mathf.Clamp01(weekAccum / SecondsPerWeekNormal);
                return;
            }

            // Survey Team days tick independently in PrologueSurveyMission; weeks keep advancing.

            // Time.deltaTime already includes timeScale, so Slow/Fast match animations & builds.
            weekAccum += Time.deltaTime;
            WeekProgress01 = Mathf.Clamp01(weekAccum / SecondsPerWeekNormal);

            if (weekAccum >= SecondsPerWeekNormal)
            {
                weekAccum = 0f;
                WeekProgress01 = 0f;
                PrologueController.Instance?.EndWeek();
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
            if (spaceLatch) return;
            spaceLatch = true;
            TogglePause();
        }

        public void TogglePause()
        {
            if (CurrentSpeed == Speed.Pause)
                SetSpeed(speedBeforePause == Speed.Pause ? Speed.Normal : speedBeforePause);
            else
            {
                speedBeforePause = CurrentSpeed;
                SetSpeed(Speed.Pause);
            }
            GameDevTV.RTS.UI.PrologueHUD.Instance?.RefreshTimeControls();
        }

        public void SetSpeed(Speed speed)
        {
            if (speed != Speed.Pause)
                speedBeforePause = speed;
            CurrentSpeed = speed;
            ApplyTimeScale();
            GameDevTV.RTS.UI.PrologueHUD.Instance?.RefreshTimeControls();
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

        public string SpeedLabel()
        {
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
