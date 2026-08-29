using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Containers;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Recovers from orphaned Time.timeScale=0 freezes (something paused the game
    /// but no overlay is actually visible). Without this, units accept Move orders
    /// (green status) but never move because deltaTime is always 0.
    /// </summary>
    public static class GameTimeScaleGuard
    {
        private static float lastRecoverLogTime = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject(nameof(GameTimeScaleGuardRunner));
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<GameTimeScaleGuardRunner>();
        }

        public static bool IsIntentionalPauseOverlayActive()
        {
            if (GenerationManager.Instance != null && GenerationManager.Instance.IsBetweenRounds)
            {
                return true;
            }

            if (BlueprintDraftUI.IsDraftVisible) return true;

            var draftingUIs = Object.FindObjectsByType<DraftingUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var ui in draftingUIs)
            {
                if (ui != null && ui.IsOverlayVisible) return true;
            }

            var summaries = Object.FindObjectsByType<GenerationSummaryUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var summary in summaries)
            {
                if (summary != null && summary.IsVisible) return true;
            }

            var discoveries = Object.FindObjectsByType<ExplorationDiscoveryUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var discovery in discoveries)
            {
                if (discovery != null && discovery.IsVisible) return true;
            }

            var pauseMenus = Object.FindObjectsByType<PauseMenuUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var pause in pauseMenus)
            {
                if (pause != null && pause.IsPauseMenuVisible) return true;
            }

            var gameOvers = Object.FindObjectsByType<GameOverUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var go in gameOvers)
            {
                if (go != null && go.IsVisible) return true;
            }

            return false;
        }

        public static bool TryRecoverOrphanedPause()
        {
            if (Time.timeScale > 0.01f) return false;
            if (IsIntentionalPauseOverlayActive()) return false;

            Time.timeScale = 1f;
            if (Time.unscaledTime - lastRecoverLogTime > 2f)
            {
                lastRecoverLogTime = Time.unscaledTime;
                Debug.LogWarning(
                    "[GameTimeScaleGuard] Recovered orphaned pause (Time.timeScale was 0 with no pause overlay). " +
                    "Units can move again.");
            }
            return true;
        }

        private sealed class GameTimeScaleGuardRunner : MonoBehaviour
        {
            private void Update()
            {
                TryRecoverOrphanedPause();
            }
        }
    }
}
