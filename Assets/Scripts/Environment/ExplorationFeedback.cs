using UnityEngine;
using GameDevTV.RTS.UI;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Ensures <see cref="PlacementErrorUI"/> exists so placement / exploration
    /// failures show near the problem instead of under the top HUD.
    /// </summary>
    public class ExplorationFeedback : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            // PlacementErrorUI self-spawns AfterSceneLoad and owns the failure events.
            if (FindAnyObjectByType<ExplorationFeedback>() != null) return;
            var go = new GameObject("ExplorationFeedback (auto)");
            go.AddComponent<ExplorationFeedback>();
            DontDestroyOnLoad(go);
        }
    }
}
