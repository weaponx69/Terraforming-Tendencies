using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Shows exploration failure messages in the HUD warning banner.
    /// </summary>
    public class ExplorationFeedback : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<ExplorationFeedback>() != null) return;
            var go = new GameObject("ExplorationFeedback (auto)");
            go.AddComponent<ExplorationFeedback>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            ExplorationManager.OnExplorationFailed += HandleExplorationFailed;
        }

        private void OnDisable()
        {
            ExplorationManager.OnExplorationFailed -= HandleExplorationFailed;
        }

        private void HandleExplorationFailed(string message)
        {
            var runtimeUi = FindAnyObjectByType<RuntimeUI>(FindObjectsInactive.Include);
            if (runtimeUi != null)
            {
                runtimeUi.ShowWarningBanner(message);
                return;
            }

            Debug.Log($"[ExplorationFeedback] {message}");
        }
    }
}
