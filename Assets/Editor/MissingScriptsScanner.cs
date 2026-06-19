using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDevTV.RTS.EditorScripts
{
    public static class MissingScriptsScanner
    {
        [MenuItem("Tools/Diagnostics/Scan for Missing Scripts")]
        public static void ScanOnly()
        {
            ExecuteScan(false);
        }

        [MenuItem("Tools/Diagnostics/Clean Missing Scripts")]
        public static void ScanAndClean()
        {
            ExecuteScan(true);
        }

        private static void ExecuteScan(bool clean)
        {
            Debug.Log($"--- Starting Missing Scripts {(clean ? "Clean" : "Scan")} ---");

            int totalPrefabsChecked = 0;
            int totalPrefabsWithMissing = 0;
            int totalPrefabScriptsRemoved = 0;

            // 1. Scan Prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                totalPrefabsChecked++;
                int missingCount = CountMissingScripts(prefab);
                if (missingCount > 0)
                {
                    totalPrefabsWithMissing++;
                    Debug.LogWarning($"[MissingScriptsScanner] Found {missingCount} missing script(s) in Prefab: {path}", prefab);

                    if (clean)
                    {
                        int removed = CleanGameObject(prefab);
                        if (removed > 0)
                        {
                            totalPrefabScriptsRemoved += removed;
                            EditorUtility.SetDirty(prefab);
                            PrefabUtility.SavePrefabAsset(prefab);
                            Debug.Log($"[MissingScriptsScanner] Cleaned {removed} missing script(s) from Prefab: {path}");
                        }
                    }
                }
            }

            int totalScenesChecked = 0;
            int totalScenesWithMissing = 0;
            int totalSceneScriptsRemoved = 0;

            // 2. Scan Scenes
            string originalScenePath = EditorSceneManager.GetActiveScene().path;
            if (clean && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[MissingScriptsScanner] Scan and clean cancelled by user (scene save prompted).");
                return;
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;

                totalScenesChecked++;
                try
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    int missingInScene = 0;

                    foreach (GameObject root in rootObjects)
                    {
                        missingInScene += CountMissingScripts(root);
                    }

                    if (missingInScene > 0)
                    {
                        totalScenesWithMissing++;
                        Debug.LogWarning($"[MissingScriptsScanner] Found {missingInScene} missing script(s) in Scene: {path}");

                        if (clean)
                        {
                            int removedInScene = 0;
                            foreach (GameObject root in rootObjects)
                            {
                                removedInScene += CleanGameObject(root);
                            }

                            if (removedInScene > 0)
                            {
                                totalSceneScriptsRemoved += removedInScene;
                                EditorSceneManager.MarkSceneDirty(scene);
                                EditorSceneManager.SaveScene(scene);
                                Debug.Log($"[MissingScriptsScanner] Cleaned {removedInScene} missing script(s) from Scene: {path}");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MissingScriptsScanner] Error scanning scene {path}: {ex.Message}");
                }
            }

            // Restore original scene
            if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
            {
                try
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MissingScriptsScanner] Error restoring original scene: {ex.Message}");
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"--- Missing Scripts Scan Summary ---");
            Debug.Log($"Prefabs checked: {totalPrefabsChecked}");
            Debug.Log($"Prefabs with missing scripts: {totalPrefabsWithMissing}");
            if (clean) Debug.Log($"Missing scripts removed from prefabs: {totalPrefabScriptsRemoved}");
            Debug.Log($"Scenes checked: {totalScenesChecked}");
            Debug.Log($"Scenes with missing scripts: {totalScenesWithMissing}");
            if (clean) Debug.Log($"Missing scripts removed from scenes: {totalSceneScriptsRemoved}");
            Debug.Log($"--- Scan & Cleanup Complete ---");
        }

        private static int CountMissingScripts(GameObject go)
        {
            int count = 0;
            Component[] components = go.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null)
                {
                    count++;
                }
            }

            foreach (Transform child in go.transform)
            {
                count += CountMissingScripts(child.gameObject);
            }

            return count;
        }

        private static int CleanGameObject(GameObject go)
        {
            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            foreach (Transform child in go.transform)
            {
                removedCount += CleanGameObject(child.gameObject);
            }

            return removedCount;
        }
    }
}
