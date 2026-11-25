using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Containers;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Playtest loop: FPS sampling, quality mitigation, and Prologue UI sanity for the UI bot.
    /// </summary>
    public static class ProloguePlaytestPerf
    {
        public const int MinFpsTarget = 30;
        public const int LowFpsStreakToAct = 4;

        static int lowFpsStreak;
        static int lastMitigationFrame;

        public static void NoteFrame(int fps)
        {
            if (fps >= MinFpsTarget)
            {
                lowFpsStreak = 0;
                return;
            }
            lowFpsStreak++;
        }

        public static bool TryMitigateLag(out string summary)
        {
            summary = "";
            if (Time.frameCount - lastMitigationFrame < 300) return false;
            if (lowFpsStreak < LowFpsStreakToAct) return false;

            lastMitigationFrame = Time.frameCount;
            lowFpsStreak = 0;
            ProloguePerformanceBudget.ApplyIfNeeded();
            summary =
                $"budget reapplied (heap {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)}MB)";
            Debug.LogWarning($"[ProloguePlaytestPerf] Lag mitigation: {summary}");
            return true;
        }

        /// <summary>JSON for shell / improve loop: fps, hud, hand strip, acts chrome leak.</summary>
        public static string UiHealthJson()
        {
            bool prologue = PrologueController.IsActive;
            bool hud = PrologueHUD.Instance != null && prologue;
            bool hand = BottomBarActionsUI.TryGetHandCardScreenPoint("Survey", out _)
                        || BottomBarActionsUI.TryGetHandCardScreenPoint("Aquifer", out _)
                        || BottomBarActionsUI.TryGetHandCardScreenPoint("Habitat", out _);
            bool actsWeeks = Object.FindAnyObjectByType<WeeksLeftUI>() is { gameObject: { activeInHierarchy: true } };
            int fps = ProloguePerformanceBudget.SmoothedFps();
            long heapMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            string uiIssue = "";
            if (prologue && !hud) uiIssue = "missing_prologue_hud";
            else if (prologue && actsWeeks) uiIssue = "acts_ui_leak";
            else if (prologue && !hand) uiIssue = "no_hand_cards";

            return
                $"{{\"prologue\":{(prologue ? "true" : "false")},\"hud\":{(hud ? "true" : "false")},\"hand\":{(hand ? "true" : "false")},\"actsLeak\":{(actsWeeks ? "true" : "false")},\"fps\":{fps},\"heapMb\":{heapMb},\"uiIssue\":\"{uiIssue}\"}}";
        }
    }
}