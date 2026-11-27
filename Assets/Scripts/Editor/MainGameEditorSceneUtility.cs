using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Unity rejects <see cref="EditorSceneManager.OpenScene"/> during SceneClosing/SceneClosed
    /// (e.g. while toggling Play). Defer opens until the editor is idle.
    /// </summary>
    public static class MainGameEditorSceneUtility
    {
        public const string MainGameScenePath = "Assets/MainGame.unity";

        [MenuItem("Terraforming/Open MainGame Scene (safe)")]
        public static void MenuOpenMainGame() => RequestOpenScene(MainGameScenePath);

        /// <summary>Schedule <paramref name="path"/> on the next idle editor frame.</summary>
        public static bool RequestOpenScene(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (SceneManager.GetActiveScene().path == path) return true;

            EditorApplication.delayCall += OpenWhenIdle;
            return true;

            void OpenWhenIdle()
            {
                if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += OpenWhenIdle;
                    return;
                }

                if (SceneManager.GetActiveScene().path == path) return;

                try
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[MainGameEditorSceneUtility] Deferred open failed for '{path}': {ex.Message}");
                }
            }
        }

        public static bool RequestOpenMainGameScene() => RequestOpenScene(MainGameScenePath);
    }
}